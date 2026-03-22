import { test, expect } from '@playwright/experimental-ct-react';
import { ChartCard } from './ChartCard';

test('renders title and children', async ({ mount }) => {
  const component = await mount(
    <ChartCard title="Duration Over Time">
      <div data-testid="chart-content">Chart here</div>
    </ChartCard>,
  );
  await expect(component.getByText('Duration Over Time')).toBeVisible();
  await expect(component.getByTestId('chart-content')).toBeVisible();
});

test('renders subtitle', async ({ mount }) => {
  const component = await mount(
    <ChartCard title="Title" subtitle="Daily average">
      <div>Chart</div>
    </ChartCard>,
  );
  await expect(component.getByText('Daily average')).toBeVisible();
});

test('renders date range label', async ({ mount }) => {
  const component = await mount(
    <ChartCard title="Title" dateRange="Last 30 days">
      <div>Chart</div>
    </ChartCard>,
  );
  await expect(component.getByText('Last 30 days')).toBeVisible();
});

test('shows View Detail button when handler is provided', async ({ mount }) => {
  const component = await mount(
    <ChartCard title="Title" onViewDetail={() => {}}>
      <div>Chart</div>
    </ChartCard>,
  );
  await expect(component.getByText('View Detail')).toBeVisible();
});

test('hides View Detail button when no handler', async ({ mount }) => {
  const component = await mount(
    <ChartCard title="Title">
      <div>Chart</div>
    </ChartCard>,
  );
  await expect(component.getByText('View Detail')).toHaveCount(0);
});

test('screenshot: chart card with subtitle and date range', async ({ mount }) => {
  const component = await mount(
    <ChartCard title="Duration Over Time" subtitle="Daily average" dateRange="Last 30 days">
      <div style={{ height: 120, background: '#f3f4f6', borderRadius: 8 }} />
    </ChartCard>,
  );
  await expect(component).toHaveScreenshot();
});
