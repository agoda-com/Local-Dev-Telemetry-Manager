import { test, expect } from '@playwright/experimental-ct-react';
import { EnvironmentToggle } from './EnvironmentToggle';

test('renders all environment tabs', async ({ mount }) => {
  const component = await mount(<EnvironmentToggle value="all" onChange={() => {}} />);
  await expect(component.getByText('All')).toBeVisible();
  await expect(component.getByText('Local')).toBeVisible();
  await expect(component.getByText('CI')).toBeVisible();
});

test('tabs are clickable', async ({ mount }) => {
  const component = await mount(<EnvironmentToggle value="all" onChange={() => {}} />);
  await component.getByText('Local').click();
  await component.getByText('CI').click();
  await component.getByText('All').click();
});
