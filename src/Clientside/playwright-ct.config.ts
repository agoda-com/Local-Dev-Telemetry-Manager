import { defineConfig, devices } from '@playwright/experimental-ct-react';
import { resolve, dirname } from 'path';
import { fileURLToPath } from 'url';

const __dirname = dirname(fileURLToPath(import.meta.url));

export default defineConfig({
  testDir: './src',
  testMatch: '**/*.spec.{ts,tsx}',
  timeout: 30_000,
  use: {
    ...devices['Desktop Chrome'],
    ctViteConfig: {
      resolve: {
        alias: {
          '@': resolve(__dirname, './src'),
        },
      },
    },
  },
});
