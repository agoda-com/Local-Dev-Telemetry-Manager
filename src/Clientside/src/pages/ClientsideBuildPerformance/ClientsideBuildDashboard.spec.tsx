import { test, expect } from '@playwright/experimental-ct-react';
import { ClientsideBuildDashboard } from './ClientsideBuildDashboard';
import {
  mockFilterOptions,
  mockClientsideBuildSummary,
  mockBuildMetricItems,
} from '../../test-fixtures/mock-data';

test.beforeEach(async ({ page }) => {
  await page.route(/\/api\//, async (route) => {
    const url = route.request().url();
    if (url.includes('/api/filters')) {
      await route.fulfill({ json: mockFilterOptions });
    } else if (url.includes('/api/build-metrics/clientside-summary')) {
      await route.fulfill({ json: mockClientsideBuildSummary });
    } else if (url.includes('/api/build-metrics')) {
      await route.fulfill({ json: mockBuildMetricItems });
    } else {
      await route.fulfill({ status: 404 });
    }
  });
});

test('renders page header', async ({ mount }) => {
  const component = await mount(<ClientsideBuildDashboard />);
  await expect(component.getByText('Clientside Build Performance')).toBeVisible();
});

test('renders KPI cards with mock data', async ({ mount }) => {
  const component = await mount(<ClientsideBuildDashboard />);
  await expect(component.getByText('Hot Reload %')).toBeVisible();
  await expect(component.getByText('Avg Hot Reload')).toBeVisible();
  await expect(component.getByText('Avg Full Reload')).toBeVisible();
  await expect(component.getByText('Total Reloads Today')).toBeVisible();
});

test('renders chart sections', async ({ mount }) => {
  const component = await mount(<ClientsideBuildDashboard />);
  await expect(component.getByText('Reload Count Over Time')).toBeVisible();
  await expect(component.getByText('Reload Time Over Time')).toBeVisible();
});

test('renders data table with build items', async ({ mount }) => {
  const component = await mount(<ClientsideBuildDashboard />);
  await expect(component.getByText('Recent Clientside Builds')).toBeVisible();
  await expect(component.getByRole('cell', { name: 'ProjectA' })).toBeVisible();
});

test('renders filter controls', async ({ mount }) => {
  const component = await mount(<ClientsideBuildDashboard />);
  await expect(component.getByRole('tab', { name: 'All' })).toBeVisible();
  await expect(component.getByRole('tab', { name: 'Local' })).toBeVisible();
  await expect(component.getByRole('tab', { name: 'CI' })).toBeVisible();
});
