import { test, expect } from '@playwright/experimental-ct-react';
import { GlobalFilters } from './GlobalFilters';
import type { FilterState } from './GlobalFilters';
import type { FilterOptions } from '../../api/client';

const defaultFilters: FilterState = {
  environment: 'all',
  platforms: [],
  projectName: '',
  dateRange: '30d',
};

const sampleOptions: FilterOptions = {
  projects: ['ProjectA', 'ProjectB'],
  repositories: ['repo-alpha'],
  branches: ['main'],
  platforms: ['Win32NT', 'Unix'],
  testRunners: ['NUnit'],
  metricTypes: ['API', 'Clientside'],
};

test('renders environment toggle tabs', async ({ mount }) => {
  const component = await mount(
    <GlobalFilters filters={defaultFilters} options={sampleOptions} onFiltersChange={() => {}} />,
  );
  await expect(component.getByRole('button', { name: 'All', exact: true })).toBeVisible();
  await expect(component.getByRole('button', { name: 'Local', exact: true })).toBeVisible();
  await expect(component.getByRole('button', { name: 'CI', exact: true })).toBeVisible();
});

test('renders project select placeholder', async ({ mount }) => {
  const component = await mount(
    <GlobalFilters filters={defaultFilters} options={sampleOptions} onFiltersChange={() => {}} />,
  );
  await expect(component.getByRole('button', { name: 'All Projects' })).toBeVisible();
});

test('renders platform multiselect placeholder', async ({ mount }) => {
  const component = await mount(
    <GlobalFilters filters={defaultFilters} options={sampleOptions} onFiltersChange={() => {}} />,
  );
  await expect(component.getByRole('button', { name: 'All Platforms' })).toBeVisible();
});

test('renders date range select with current value', async ({ mount }) => {
  const component = await mount(
    <GlobalFilters filters={defaultFilters} options={sampleOptions} onFiltersChange={() => {}} />,
  );
  await expect(component.getByRole('button', { name: 'Last 30 days' })).toBeVisible();
});

test('screenshot: default filter state', async ({ mount }) => {
  const component = await mount(
    <GlobalFilters filters={defaultFilters} options={sampleOptions} onFiltersChange={() => {}} />,
  );
  await expect(component).toHaveScreenshot();
});
