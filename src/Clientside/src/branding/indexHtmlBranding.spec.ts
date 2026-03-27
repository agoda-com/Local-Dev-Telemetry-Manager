import { test, expect } from '@playwright/experimental-ct-react';
import { readFileSync } from 'fs';
import { resolve } from 'path';

test('index.html uses DX title', async () => {
  const html = readFileSync(resolve(process.cwd(), 'index.html'), 'utf8');
  expect(html).toContain('<title>DX Telemetry Dashboard</title>');
});

test('index.html includes favicon links (svg + png fallback)', async () => {
  const html = readFileSync(resolve(process.cwd(), 'index.html'), 'utf8');
  expect(html).toContain('rel="icon" type="image/svg+xml" href="/favicon.svg"');
  expect(html).toContain('rel="alternate icon" type="image/png" href="/favicon.png"');
});
