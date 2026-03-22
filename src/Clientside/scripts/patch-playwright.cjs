/**
 * Patches @playwright/experimental-ct-core for two bugs in v1.58.2:
 *
 * 1. ct-core/index.js: defineConfig wrapper uses object spread which drops
 *    Symbol(defineConfigWasUsed) set by the base defineConfig. We add a loop
 *    to copy symbols from the base result.
 *
 * 2. ct-core/lib/mount.js: The defineConfigWasUsed check fails in ESM/CJS
 *    dual-module contexts where the Symbol instance differs. Since the config
 *    is validated elsewhere, we remove this redundant guard.
 */

const fs = require('fs');
const path = require('path');

const ctCorePkg = path.join(
  __dirname, '..', 'node_modules', '@playwright', 'experimental-ct-core',
);

let patchedCount = 0;

// Patch 1: preserve Symbol properties in defineConfig wrapper
const indexPath = path.join(ctCorePkg, 'index.js');
let indexSrc = fs.readFileSync(indexPath, 'utf-8');

const oldSpread = `  return {
    ...original,
    '@playwright/test': {
      ...original['@playwright/test'],
      plugins: [() => createPlugin()],
      babelPlugins: [
        [require.resolve('./lib/tsxTransform')]
      ],
    }
  };`;

const newSpread = `  const result = {
    ...original,
    '@playwright/test': {
      ...original['@playwright/test'],
      plugins: [() => createPlugin()],
      babelPlugins: [
        [require.resolve('./lib/tsxTransform')]
      ],
    }
  };
  for (const sym of Object.getOwnPropertySymbols(original)) {
    result[sym] = original[sym];
  }
  return result;`;

if (indexSrc.includes(oldSpread)) {
  indexSrc = indexSrc.replace(oldSpread, newSpread);
  fs.writeFileSync(indexPath, indexSrc, 'utf-8');
  console.log('[patch-playwright] Patched ct-core/index.js (Symbol preservation)');
  patchedCount++;
} else if (indexSrc.includes('getOwnPropertySymbols')) {
  console.log('[patch-playwright] ct-core/index.js already patched');
  patchedCount++;
} else {
  console.error('[patch-playwright] FATAL: Could not find expected code in ct-core/index.js');
  console.error('[patch-playwright] Playwright CT tests will fail. Update the patch for the installed version.');
  process.exit(1);
}

// Patch 2: remove defineConfigWasUsed guard in mount.js
const mountPath = path.join(ctCorePkg, 'lib', 'mount.js');
let mountSrc = fs.readFileSync(mountPath, 'utf-8');

const guardLine =
  '    if (!info._configInternal.defineConfigWasUsed)\n' +
  '      throw new Error("Component testing requires the use of the defineConfig() in your playwright-ct.config.{ts,js}: https://aka.ms/playwright/ct-define-config");';

if (mountSrc.includes(guardLine)) {
  mountSrc = mountSrc.replace(guardLine, '');
  fs.writeFileSync(mountPath, mountSrc, 'utf-8');
  console.log('[patch-playwright] Patched ct-core/lib/mount.js (removed defineConfig guard)');
  patchedCount++;
} else if (!mountSrc.includes('defineConfigWasUsed')) {
  console.log('[patch-playwright] ct-core/lib/mount.js already patched');
  patchedCount++;
} else {
  console.error('[patch-playwright] FATAL: Could not find expected guard in ct-core/lib/mount.js');
  console.error('[patch-playwright] Playwright CT tests will fail. Update the patch for the installed version.');
  process.exit(1);
}

console.log(`[patch-playwright] Done (${patchedCount}/2 patches verified)`);
