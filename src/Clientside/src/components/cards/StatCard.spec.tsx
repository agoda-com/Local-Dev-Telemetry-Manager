import { test, expect } from '@playwright/experimental-ct-react';
import { StatCard } from './StatCard';

test('renders title and numeric value', async ({ mount }) => {
  const component = await mount(<StatCard title="Total Runs" value={142} />);
  await expect(component.getByText('Total Runs')).toBeVisible();
  await expect(component.getByText('142')).toBeVisible();
});

test('renders string value', async ({ mount }) => {
  const component = await mount(<StatCard title="Duration" value="34.5s" />);
  await expect(component.getByText('34.5s')).toBeVisible();
});

test('renders subtitle', async ({ mount }) => {
  const component = await mount(<StatCard title="Metric" value={100} subtitle="vs last week" />);
  await expect(component.getByText('vs last week')).toBeVisible();
});

test('renders trend badge with value', async ({ mount }) => {
  const component = await mount(
    <StatCard title="Pass Rate" value="95.3%" trend="increase" trendValue="+2.1%" />,
  );
  await expect(component.getByText('+2.1%')).toBeVisible();
});

test('renders without crashing when sparkline data is provided', async ({ mount }) => {
  const sparklineData = [
    { date: '2026-03-15', value: 100 },
    { date: '2026-03-16', value: 110 },
    { date: '2026-03-17', value: 105 },
  ];
  const component = await mount(
    <StatCard title="Metric" value="100" sparklineData={sparklineData} />,
  );
  await expect(component.getByText('Metric')).toBeVisible();
});

test('screenshot: basic stat card', async ({ mount }) => {
  const component = await mount(<StatCard title="Total Runs" value={142} />);
  await expect(component).toHaveScreenshot();
});

test('screenshot: stat card with trend', async ({ mount }) => {
  const component = await mount(
    <StatCard title="Pass Rate" value="95.3%" trend="increase" trendValue="+2.1%" />,
  );
  await expect(component).toHaveScreenshot();
});
