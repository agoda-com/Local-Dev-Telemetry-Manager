import { RiTimeLine, RiSpeedLine, RiCheckboxCircleLine, RiCloseCircleLine } from '@remixicon/react';
import type { ColumnDef } from '@tanstack/react-table';
import { useNavigate } from 'react-router-dom';
import { PageHeader } from '../../components/layout/PageHeader';
import { DashboardGrid } from '../../components/layout/DashboardGrid';
import { StatCard } from '../../components/cards/StatCard';
import { ChartCard } from '../../components/cards/ChartCard';
import { TrendLineChart } from '../../components/charts/TrendLineChart';
import { TrendBarChart } from '../../components/charts/TrendBarChart';
import { GlobalFilters } from '../../components/filters/GlobalFilters';
import { DataTable } from '../../components/tables/DataTable';
import { useFilters } from '../../hooks/useFilters';
import { useTestRunSummary } from '../../hooks/useTestRunSummary';
import { formatMs, formatPercent } from '../../utils/format';
import type { TestRunItem } from '../../api/client';

const runColumns: ColumnDef<TestRunItem, unknown>[] = [
  { accessorKey: 'projectName', header: 'Project' },
  { accessorKey: 'testRunner', header: 'Runner' },
  { accessorKey: 'totalTests', header: 'Total' },
  { accessorKey: 'passedTests', header: 'Passed' },
  { accessorKey: 'failedTests', header: 'Failed' },
  {
    accessorKey: 'totalDurationMs',
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

export function TestRunDashboard() {
  const { filters, options, setFilters } = useFilters();
  const { summary, runs, loading, page, setPage } = useTestRunSummary(filters);
  const navigate = useNavigate();

  const totalFailures = summary?.passFailTrend.reduce((sum, d) => sum + d.failed, 0) ?? 0;

  if (loading && !summary) {
    return <PageHeader title="Test Run Performance" subtitle="Loading..." />;
  }

  return (
    <>
      <PageHeader title="Test Run Performance" />
      <GlobalFilters filters={filters} options={options} onFiltersChange={setFilters} />

      <DashboardGrid columns={4}>
        <StatCard title="Total Runs" value={summary?.totalRuns ?? 0} icon={RiTimeLine} />
        <StatCard
          title="Avg Duration"
          value={formatMs(summary?.avgDurationMs)}
          icon={RiSpeedLine}
          iconTint="amber"
        />
        <StatCard
          title="Pass Rate"
          value={formatPercent(summary?.passRate)}
          icon={RiCheckboxCircleLine}
          iconTint="green"
        />
        <StatCard title="Failures" value={totalFailures} icon={RiCloseCircleLine} iconTint="rose" />
      </DashboardGrid>

      <DashboardGrid columns={2}>
        <ChartCard title="Duration Over Time">
          <TrendLineChart
            data={summary?.durationTrend ?? []}
            index="date"
            categories={['value']}
            colors={['blue']}
            showLegend={false}
          />
        </ChartCard>
        <ChartCard title="Pass / Fail Ratio">
          <TrendBarChart
            data={summary?.passFailTrend ?? []}
            index="date"
            categories={['passed', 'failed']}
            colors={['emerald', 'rose']}
            stack
          />
        </ChartCard>
      </DashboardGrid>

      <ChartCard title="Recent Test Runs">
        <DataTable
          columns={[
            ...runColumns,
            {
              id: 'actions',
              header: '',
              cell: (info) => (
                <button
                  onClick={() => navigate(`/test-runs/${info.row.original.id}`)}
                  className="text-sm font-medium text-brand-500 hover:text-brand-600 transition-colors"
                >
                  Detail
                </button>
              ),
            },
          ]}
          data={runs?.items ?? []}
          totalCount={runs?.totalCount}
          page={page}
          pageSize={20}
          onPageChange={setPage}
        />
      </ChartCard>
    </>
  );
}
