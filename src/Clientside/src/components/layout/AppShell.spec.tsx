import { test, expect } from '@playwright/experimental-ct-react';
import { AppShell } from './AppShell';

test('renders header logo', async ({ mount }) => {
  const component = await mount(
    <AppShell>
      <div>Content</div>
    </AppShell>,
    { hooksConfig: { enableRouting: true } },
  );
  await expect(component.getByRole('img', { name: 'DX Telemetry' })).toBeVisible();
});

test('renders all navigation tabs', async ({ mount }) => {
  const component = await mount(
    <AppShell>
      <div>Content</div>
    </AppShell>,
    { hooksConfig: { enableRouting: true } },
  );
  await expect(component.getByRole('button', { name: 'Test Runs' })).toBeVisible();
  await expect(component.getByRole('button', { name: 'API Build' })).toBeVisible();
  await expect(component.getByRole('button', { name: 'Clientside Build' })).toBeVisible();
});

test('renders children in main area', async ({ mount }) => {
  const component = await mount(
    <AppShell>
      <div data-testid="page-content">Page Content</div>
    </AppShell>,
    { hooksConfig: { enableRouting: true } },
  );
  await expect(component.getByTestId('page-content')).toBeVisible();
});

test('screenshot: shell with content', async ({ mount }) => {
  const component = await mount(
    <AppShell>
      <div style={{ padding: 24 }}>Sample page content</div>
    </AppShell>,
    { hooksConfig: { enableRouting: true } },
  );
  await expect(component).toHaveScreenshot();
});
