import { test, expect } from '@playwright/experimental-ct-react';
import { TestRunDashboard } from './TestRunDashboard';
import {
  mockFilterOptions,
  mockTestRunSummary,
  mockTestRunItems,
} from '../../test-fixtures/mock-data';

test.beforeEach(async ({ page }) => {
  await page.route(/\/api\//, async (route) => {
    const url = route.request().url();
    if (url.includes('/api/filters')) {
      await route.fulfill({ json: mockFilterOptions });
    } else if (url.includes('/api/test-runs/summary')) {
      await route.fulfill({ json: mockTestRunSummary });
    } else if (url.includes('/api/test-runs')) {
      await route.fulfill({ json: mockTestRunItems });
    } else {
      await route.fulfill({ status: 404 });
    }
  });
});

test('renders page header', async ({ mount }) => {
  const component = await mount(<TestRunDashboard />, {
    hooksConfig: { enableRouting: true },
  });
  await expect(component.getByText('Test Run Performance')).toBeVisible();
});

test('renders KPI cards with mock data', async ({ mount }) => {
  const component = await mount(<TestRunDashboard />, {
    hooksConfig: { enableRouting: true },
  });
  await expect(component.getByText('Total Runs')).toBeVisible();
  await expect(component.getByText('142')).toBeVisible();
  await expect(component.getByText('Avg Duration')).toBeVisible();
  await expect(component.getByText('Pass Rate')).toBeVisible();
  await expect(component.getByText('Failures')).toBeVisible();
});

test('renders chart sections', async ({ mount }) => {
  const component = await mount(<TestRunDashboard />, {
    hooksConfig: { enableRouting: true },
  });
  await expect(component.getByText('Duration Over Time')).toBeVisible();
  await expect(component.getByText('Pass / Fail Ratio')).toBeVisible();
});

test('renders data table with test run items', async ({ mount }) => {
  const component = await mount(<TestRunDashboard />, {
    hooksConfig: { enableRouting: true },
  });
  await expect(component.getByText('Recent Test Runs')).toBeVisible();
  await expect(component.getByRole('cell', { name: 'ProjectA' })).toBeVisible();
  await expect(component.getByRole('cell', { name: 'ProjectB' })).toBeVisible();
});

test('renders filter controls', async ({ mount }) => {
  const component = await mount(<TestRunDashboard />, {
    hooksConfig: { enableRouting: true },
  });
  await expect(component.getByRole('button', { name: 'All', exact: true })).toBeVisible();
  await expect(component.getByRole('button', { name: 'Local', exact: true })).toBeVisible();
  await expect(component.getByRole('button', { name: 'CI', exact: true })).toBeVisible();
});

test('screenshot: full dashboard', async ({ mount }) => {
  const component = await mount(<TestRunDashboard />, {
    hooksConfig: { enableRouting: true },
  });
  await expect(component.getByText('Recent Test Runs')).toBeVisible();
  await expect(component).toHaveScreenshot({ maxDiffPixelRatio: 0.01 });
});
