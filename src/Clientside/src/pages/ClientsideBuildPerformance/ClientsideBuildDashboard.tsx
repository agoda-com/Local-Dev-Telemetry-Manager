import { LineChart, BarChart } from '@tremor/react';
import { RiFlashlightLine, RiRefreshLine, RiLoaderLine, RiBarChartLine } from '@remixicon/react';
import type { ColumnDef } from '@tanstack/react-table';
import { PageHeader } from '../../components/layout/PageHeader';
import { DashboardGrid } from '../../components/layout/DashboardGrid';
import { StatCard } from '../../components/cards/StatCard';
import { ChartCard } from '../../components/cards/ChartCard';
import { GlobalFilters } from '../../components/filters/GlobalFilters';
import { DataTable } from '../../components/tables/DataTable';
import { useFilters } from '../../hooks/useFilters';
import { useClientsideBuildMetrics } from '../../hooks/useClientsideBuildMetrics';
import { formatMs, formatPercent } from '../../utils/format';
import type { BuildMetricItem } from '../../api/client';

const buildColumns: ColumnDef<BuildMetricItem, unknown>[] = [
  { accessorKey: 'projectName', header: 'Project' },
  { accessorKey: 'metricType', header: 'Type' },
  { accessorKey: 'reloadType', header: 'Reload' },
  {
    accessorKey: 'timeTakenMs',
    header: 'Duration',
    cell: (info) => formatMs(info.getValue() as number),
  },
  { accessorKey: 'executionEnvironment', header: 'Env' },
  {
    accessorKey: 'receivedAt',
    header: 'Received',
    cell: (info) => (info.getValue() as string).slice(0, 10),
  },
];

export function ClientsideBuildDashboard() {
  const { filters, options, setFilters } = useFilters();
  const { data, builds, loading, page, setPage } = useClientsideBuildMetrics(filters);

  if (loading && !data) {
    return <PageHeader title="Clientside Build Performance" subtitle="Loading..." />;
  }

  return (
    <>
      <PageHeader title="Clientside Build Performance" />
      <GlobalFilters filters={filters} options={options} onFiltersChange={setFilters} />

      <DashboardGrid columns={4}>
        <StatCard
          title="Hot Reload %"
          value={formatPercent(data?.hotReloadPercent != null ? data.hotReloadPercent / 100 : null)}
          icon={RiFlashlightLine}
        />
        <StatCard
          title="Avg Hot Reload"
          value={formatMs(data?.avgHotReloadTimeMs)}
          icon={RiRefreshLine}
        />
        <StatCard
          title="Avg Full Reload"
          value={formatMs(data?.avgFullReloadTimeMs)}
          icon={RiLoaderLine}
        />
        <StatCard
          title="Total Reloads Today"
          value={data?.totalReloadsToday ?? 0}
          icon={RiBarChartLine}
        />
      </DashboardGrid>

      <DashboardGrid columns={2}>
        <ChartCard title="Reload Count Over Time">
          <BarChart
            data={data?.reloadCountTrend ?? []}
            index="date"
            categories={['hotReloads', 'fullReloads']}
            colors={['emerald', 'blue']}
            stack
          />
        </ChartCard>
        <ChartCard title="Reload Time Over Time">
          <LineChart
            data={data?.reloadTimeTrend ?? []}
            index="date"
            categories={['avgHotTimeMs', 'avgFullTimeMs']}
            colors={['emerald', 'blue']}
            yAxisWidth={56}
          />
        </ChartCard>
      </DashboardGrid>

      <ChartCard title="Recent Clientside Builds">
        <DataTable
          columns={buildColumns}
          data={builds?.items ?? []}
          totalCount={builds?.totalCount}
          page={page}
          pageSize={20}
          onPageChange={setPage}
        />
      </ChartCard>
    </>
  );
}
