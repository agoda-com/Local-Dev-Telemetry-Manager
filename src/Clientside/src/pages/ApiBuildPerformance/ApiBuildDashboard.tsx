import { LineChart, BarChart } from '@tremor/react';
import { RiCodeLine, RiRocketLine, RiFlashlightLine } from '@remixicon/react';
import type { ColumnDef } from '@tanstack/react-table';
import { PageHeader } from '../../components/layout/PageHeader';
import { DashboardGrid } from '../../components/layout/DashboardGrid';
import { StatCard } from '../../components/cards/StatCard';
import { ChartCard } from '../../components/cards/ChartCard';
import { GlobalFilters } from '../../components/filters/GlobalFilters';
import { DataTable } from '../../components/tables/DataTable';
import { useFilters } from '../../hooks/useFilters';
import { useApiBuildMetrics } from '../../hooks/useApiBuildMetrics';
import { formatMs } from '../../utils/format';
import type { BuildMetricItem } from '../../api/client';

const buildColumns: ColumnDef<BuildMetricItem, unknown>[] = [
  { accessorKey: 'projectName', header: 'Project' },
  { accessorKey: 'metricType', header: 'Type' },
  {
    accessorKey: 'timeTakenMs',
    header: 'Duration',
    cell: info => formatMs(info.getValue() as number),
  },
  { accessorKey: 'executionEnvironment', header: 'Env' },
  { accessorKey: 'receivedAt', header: 'Received', cell: info => (info.getValue() as string).slice(0, 10) },
];

export function ApiBuildDashboard() {
  const { filters, options, setFilters } = useFilters();
  const { data, builds, loading, page, setPage } = useApiBuildMetrics(filters);

  if (loading && !data) {
    return <PageHeader title="API Build Performance" subtitle="Loading..." />;
  }

  return (
    <>
      <PageHeader title="API Build Performance" />
      <GlobalFilters filters={filters} options={options} onFiltersChange={setFilters} />

      <DashboardGrid columns={3}>
        <StatCard
          title="Avg Compile Time"
          value={formatMs(data?.avgCompileTimeMs)}
          icon={RiCodeLine}
          sparklineData={data?.compileTrend}
        />
        <StatCard
          title="Avg Startup Time"
          value={formatMs(data?.avgStartupTimeMs)}
          icon={RiRocketLine}
          sparklineData={data?.startupTrend}
        />
        <StatCard
          title="Avg First Response"
          value={formatMs(data?.avgFirstResponseTimeMs)}
          icon={RiFlashlightLine}
          sparklineData={data?.firstResponseTrend}
        />
      </DashboardGrid>

      <DashboardGrid columns={2}>
        <ChartCard title="Build Lifecycle Over Time">
          <LineChart
            data={data?.dailyLifecycle ?? []}
            index="date"
            categories={['avgCompileMs', 'avgStartupMs', 'avgFirstResponseMs']}
            colors={['blue', 'emerald', 'amber']}
            yAxisWidth={56}
          />
        </ChartCard>
        <ChartCard title="Lifecycle Breakdown">
          <BarChart
            data={data?.dailyLifecycle ?? []}
            index="date"
            categories={['avgCompileMs', 'avgStartupMs', 'avgFirstResponseMs']}
            colors={['blue', 'emerald', 'amber']}
            stack
          />
        </ChartCard>
      </DashboardGrid>

      <ChartCard title="Recent API Builds">
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
