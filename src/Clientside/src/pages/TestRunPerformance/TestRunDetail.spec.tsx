import { test, expect } from '@playwright/experimental-ct-react';
import { TestRunDetail } from './TestRunDetail';
import { mockTestRunDetail } from '../../test-fixtures/mock-data';

test.beforeEach(async ({ page }) => {
  await page.route(/\/api\/test-runs\//, async (route) => {
    await route.fulfill({ json: mockTestRunDetail });
  });
});

test('renders page header with project name', async ({ mount }) => {
  const component = await mount(<TestRunDetail />, {
    hooksConfig: { routePath: '/test-runs/:id', routeUrl: '/test-runs/run-1' },
  });
  await expect(component.getByText('Test Run: ProjectA')).toBeVisible();
});

test('renders metadata cards', async ({ mount }) => {
  const component = await mount(<TestRunDetail />, {
    hooksConfig: { routePath: '/test-runs/:id', routeUrl: '/test-runs/run-1' },
  });
  await expect(component.getByText('jdickson')).toBeVisible();
  await expect(component.getByText('dev-machine')).toBeVisible();
  await expect(component.getByText('feature/testing')).toBeVisible();
});

test('renders test case table', async ({ mount }) => {
  const component = await mount(<TestRunDetail />, {
    hooksConfig: { routePath: '/test-runs/:id', routeUrl: '/test-runs/run-1' },
  });
  await expect(component.getByText('All Test Cases')).toBeVisible();
  await expect(component.getByRole('cell', { name: 'TestOne' }).first()).toBeVisible();
  await expect(component.getByRole('cell', { name: 'TestThree' }).first()).toBeVisible();
});

test('renders slowest tests section', async ({ mount }) => {
  const component = await mount(<TestRunDetail />, {
    hooksConfig: { routePath: '/test-runs/:id', routeUrl: '/test-runs/run-1' },
  });
  await expect(component.getByText('Slowest Tests (Top 10)')).toBeVisible();
});

test('renders back link', async ({ mount }) => {
  const component = await mount(<TestRunDetail />, {
    hooksConfig: { routePath: '/test-runs/:id', routeUrl: '/test-runs/run-1' },
  });
  await expect(component.getByText('← Back to runs')).toBeVisible();
});

test('renders status filter buttons', async ({ mount }) => {
  const component = await mount(<TestRunDetail />, {
    hooksConfig: { routePath: '/test-runs/:id', routeUrl: '/test-runs/run-1' },
  });
  await expect(component.getByRole('button', { name: 'All' })).toBeVisible();
  await expect(component.getByRole('button', { name: 'Passed' })).toBeVisible();
  await expect(component.getByRole('button', { name: 'Failed' })).toBeVisible();
  await expect(component.getByRole('button', { name: 'Skipped' })).toBeVisible();
});

test('filters test cases by status when clicking filter button', async ({ mount }) => {
  const component = await mount(<TestRunDetail />, {
    hooksConfig: { routePath: '/test-runs/:id', routeUrl: '/test-runs/run-1' },
  });
  await component.getByRole('button', { name: 'Failed' }).click();
  await expect(component.getByRole('cell', { name: 'TestThree' }).first()).toBeVisible();
});
