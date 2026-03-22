import { test, expect } from '@playwright/experimental-ct-react';
import { PageHeader } from './PageHeader';

test('renders title', async ({ mount }) => {
  const component = await mount(<PageHeader title="Test Title" />);
  await expect(component.getByRole('heading', { name: 'Test Title' })).toBeVisible();
});

test('renders subtitle when provided', async ({ mount }) => {
  const component = await mount(<PageHeader title="Title" subtitle="Subtitle text" />);
  await expect(component.getByText('Subtitle text')).toBeVisible();
});

test('omits subtitle element when not provided', async ({ mount }) => {
  const component = await mount(<PageHeader title="Title Only" />);
  await expect(component.getByRole('heading', { name: 'Title Only' })).toBeVisible();
  await expect(component.locator('p')).toHaveCount(0);
});

test('screenshot: title only', async ({ mount }) => {
  const component = await mount(<PageHeader title="Dashboard" />);
  await expect(component).toHaveScreenshot();
});

test('screenshot: title with subtitle', async ({ mount }) => {
  const component = await mount(<PageHeader title="Dashboard" subtitle="Overview of metrics" />);
  await expect(component).toHaveScreenshot();
});
