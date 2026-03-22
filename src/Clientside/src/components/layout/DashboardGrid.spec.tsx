import { test, expect } from '@playwright/experimental-ct-react';
import { DashboardGrid } from './DashboardGrid';

test('renders children', async ({ mount }) => {
  const component = await mount(
    <DashboardGrid columns={2}>
      <div data-testid="child-1">Child 1</div>
      <div data-testid="child-2">Child 2</div>
    </DashboardGrid>,
  );
  await expect(component.getByTestId('child-1')).toBeVisible();
  await expect(component.getByTestId('child-2')).toBeVisible();
});

test('applies 4-column grid classes', async ({ mount }) => {
  const component = await mount(
    <DashboardGrid columns={4}>
      <div>Item</div>
    </DashboardGrid>,
  );
  await expect(component).toHaveClass(/grid-cols-1/);
  await expect(component).toHaveClass(/lg:grid-cols-4/);
});

test('applies 3-column grid classes', async ({ mount }) => {
  const component = await mount(
    <DashboardGrid columns={3}>
      <div>Item</div>
    </DashboardGrid>,
  );
  await expect(component).toHaveClass(/lg:grid-cols-3/);
});

test('applies 1-column grid class', async ({ mount }) => {
  const component = await mount(
    <DashboardGrid columns={1}>
      <div>Item</div>
    </DashboardGrid>,
  );
  await expect(component).toHaveClass(/grid-cols-1/);
});

test('screenshot: 4-column grid', async ({ mount }) => {
  const component = await mount(
    <DashboardGrid columns={4}>
      <div style={{ padding: 16, background: '#e5e7eb' }}>Card 1</div>
      <div style={{ padding: 16, background: '#e5e7eb' }}>Card 2</div>
      <div style={{ padding: 16, background: '#e5e7eb' }}>Card 3</div>
      <div style={{ padding: 16, background: '#e5e7eb' }}>Card 4</div>
    </DashboardGrid>,
  );
  await expect(component).toHaveScreenshot();
});
