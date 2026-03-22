import { test, expect } from '@playwright/experimental-ct-react';
import type { ColumnDef } from '@tanstack/react-table';
import { DataTable } from './DataTable';

interface TestRow {
  name: string;
  value: number;
}

const columns: ColumnDef<TestRow, unknown>[] = [
  { accessorKey: 'name', header: 'Name' },
  { accessorKey: 'value', header: 'Value' },
];

const data: TestRow[] = [
  { name: 'Alpha', value: 10 },
  { name: 'Bravo', value: 20 },
  { name: 'Charlie', value: 30 },
];

test('renders column headers', async ({ mount }) => {
  const component = await mount(<DataTable columns={columns} data={data} />);
  await expect(component.getByText('Name')).toBeVisible();
  await expect(component.getByText('Value')).toBeVisible();
});

test('renders all data rows', async ({ mount }) => {
  const component = await mount(<DataTable columns={columns} data={data} />);
  await expect(component.getByText('Alpha')).toBeVisible();
  await expect(component.getByText('Bravo')).toBeVisible();
  await expect(component.getByText('Charlie')).toBeVisible();
});

test('shows empty state when data array is empty', async ({ mount }) => {
  const component = await mount(<DataTable columns={columns} data={[]} />);
  await expect(component.getByText('No data available')).toBeVisible();
});

test('renders pagination controls when totalCount exceeds pageSize', async ({ mount }) => {
  const component = await mount(
    <DataTable
      columns={columns}
      data={data}
      totalCount={50}
      page={1}
      pageSize={20}
      onPageChange={() => {}}
    />,
  );
  await expect(component.getByText('Previous')).toBeVisible();
  await expect(component.getByText('Next')).toBeVisible();
});

test('disables Previous button on first page', async ({ mount }) => {
  const component = await mount(
    <DataTable
      columns={columns}
      data={data}
      totalCount={50}
      page={1}
      pageSize={20}
      onPageChange={() => {}}
    />,
  );
  await expect(component.getByText('Previous')).toBeDisabled();
  await expect(component.getByText('Next')).toBeEnabled();
});

test('disables Next button on last page', async ({ mount }) => {
  const component = await mount(
    <DataTable
      columns={columns}
      data={data}
      totalCount={50}
      page={3}
      pageSize={20}
      onPageChange={() => {}}
    />,
  );
  await expect(component.getByText('Previous')).toBeEnabled();
  await expect(component.getByText('Next')).toBeDisabled();
});

test('hides pagination when totalCount fits in one page', async ({ mount }) => {
  const component = await mount(
    <DataTable
      columns={columns}
      data={data}
      totalCount={3}
      page={1}
      pageSize={20}
      onPageChange={() => {}}
    />,
  );
  await expect(component.getByText('Previous')).toHaveCount(0);
});

test('column headers are clickable for sorting', async ({ mount }) => {
  const component = await mount(<DataTable columns={columns} data={data} />);
  await component.getByText('Name').click();
  await component.getByText('Value').click();
});
