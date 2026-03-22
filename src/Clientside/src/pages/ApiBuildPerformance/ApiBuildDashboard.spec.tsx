import { test, expect } from '@playwright/experimental-ct-react';
import { ApiBuildDashboard } from './ApiBuildDashboard';
import {
  mockFilterOptions,
  mockApiBuildSummary,
  mockBuildMetricItems,
} from '../../test-fixtures/mock-data';

test.beforeEach(async ({ page }) => {
  await page.route(/\/api\//, async (route) => {
    const url = route.request().url();
    if (url.includes('/api/filters')) {
      await route.fulfill({ json: mockFilterOptions });
    } else if (url.includes('/api/build-metrics/api-summary')) {
      await route.fulfill({ json: mockApiBuildSummary });
    } else if (url.includes('/api/build-metrics')) {
      await route.fulfill({ json: mockBuildMetricItems });
    } else {
      await route.fulfill({ status: 404 });
    }
  });
});

test('renders page header', async ({ mount }) => {
  const component = await mount(<ApiBuildDashboard />);
  await expect(component.getByText('API Build Performance')).toBeVisible();
});

test('renders KPI cards with mock data', async ({ mount }) => {
  const component = await mount(<ApiBuildDashboard />);
  await expect(component.getByText('Avg Compile Time')).toBeVisible();
  await expect(component.getByText('Avg Startup Time')).toBeVisible();
  await expect(component.getByText('Avg First Response')).toBeVisible();
});

test('renders chart sections', async ({ mount }) => {
  const component = await mount(<ApiBuildDashboard />);
  await expect(component.getByText('Build Lifecycle Over Time')).toBeVisible();
  await expect(component.getByText('Lifecycle Breakdown')).toBeVisible();
});

test('renders data table with build items', async ({ mount }) => {
  const component = await mount(<ApiBuildDashboard />);
  await expect(component.getByText('Recent API Builds')).toBeVisible();
  await expect(component.getByRole('cell', { name: 'ProjectA' })).toBeVisible();
});

test('renders filter controls', async ({ mount }) => {
  const component = await mount(<ApiBuildDashboard />);
  await expect(component.getByRole('tab', { name: 'All' })).toBeVisible();
  await expect(component.getByRole('tab', { name: 'Local' })).toBeVisible();
  await expect(component.getByRole('tab', { name: 'CI' })).toBeVisible();
});

test('screenshot: full dashboard', async ({ mount }) => {
  const component = await mount(<ApiBuildDashboard />);
  await expect(component.getByText('Recent API Builds')).toBeVisible();
  await expect(component).toHaveScreenshot({ maxDiffPixelRatio: 0.01 });
});
