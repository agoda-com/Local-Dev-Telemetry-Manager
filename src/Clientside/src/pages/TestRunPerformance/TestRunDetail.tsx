import { useState, useEffect, useMemo } from 'react';
import { useParams, Link } from 'react-router-dom';
import { Badge } from '@tremor/react';
import type { ColumnDef } from '@tanstack/react-table';
import { PageHeader } from '../../components/layout/PageHeader';
import { DashboardGrid } from '../../components/layout/DashboardGrid';
import { ChartCard } from '../../components/cards/ChartCard';
import { DataTable } from '../../components/tables/DataTable';
import { formatMs } from '../../utils/format';
import {
  fetchTestRunDetail,
  type TestRunDetail as TestRunDetailData,
  type TestCaseItem,
} from '../../api/client';

const STATUS_COLORS: Record<string, string> = {
  passed: 'emerald',
  failed: 'rose',
  skipped: 'gray',
};

const testCaseColumns: ColumnDef<TestCaseItem, unknown>[] = [
  { accessorKey: 'name', header: 'Test Name' },
  { accessorKey: 'className', header: 'Class' },
  {
    accessorKey: 'status',
    header: 'Status',
    cell: (info) => {
      const status = (info.getValue() as string) ?? '';
      const color = STATUS_COLORS[status.toLowerCase()] ?? 'gray';
      return <Badge color={color}>{status}</Badge>;
    },
  },
  {
    accessorKey: 'durationMs',
    header: 'Duration',
    cell: (info) => formatMs(info.getValue() as number | null),
  },
  {
    accessorKey: 'errorMessage',
    header: 'Error',
    cell: (info) => {
      const msg = info.getValue() as string | null;
      if (!msg) return null;
      return (
        <span className="text-sm text-rose-500" title={msg}>
          {msg.length > 80 ? `${msg.slice(0, 80)}…` : msg}
        </span>
      );
    },
  },
];

export function TestRunDetail() {
  const { id } = useParams<{ id: string }>();
  const [detail, setDetail] = useState<TestRunDetailData | null>(null);
  const [loading, setLoading] = useState(true);
  const [statusFilter, setStatusFilter] = useState('all');

  useEffect(() => {
    if (!id) return;
    let cancelled = false;
    fetchTestRunDetail(id)
      .then((d) => {
        if (!cancelled) setDetail(d);
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [id]);

  const filteredCases = useMemo(() => {
    if (!detail) return [];
    if (statusFilter === 'all') return detail.testCases;
    return detail.testCases.filter((tc) => tc.status.toLowerCase() === statusFilter);
  }, [detail, statusFilter]);

  const slowestTests = useMemo(() => {
    if (!detail) return [];
    return [...detail.testCases]
      .sort((a, b) => (b.durationMs ?? 0) - (a.durationMs ?? 0))
      .slice(0, 10);
  }, [detail]);

  if (loading) {
    return <PageHeader title="Test Run Detail" subtitle="Loading..." />;
  }

  if (!detail) {
    return <PageHeader title="Test Run Detail" subtitle="Run not found" />;
  }

  return (
    <>
      <PageHeader
        title={`Test Run: ${detail.projectName}`}
        subtitle={`${detail.testRunner} · ${detail.executionEnvironment}`}
      />

      <Link
        to="/test-runs"
        className="inline-flex items-center gap-1 text-sm font-medium text-brand-500 hover:text-brand-600 transition-colors mb-6"
      >
        ← Back to runs
      </Link>

      <DashboardGrid columns={3}>
        <MetadataCard label="User" value={detail.userName} />
        <MetadataCard label="Host" value={detail.hostname} />
        <MetadataCard label="Branch" value={detail.branch} />
        <MetadataCard label="Project" value={detail.projectName} />
        <MetadataCard label="Runner" value={detail.testRunner} />
        <MetadataCard label="Environment" value={detail.executionEnvironment} />
        <MetadataCard label="Total Tests" value={String(detail.totalTests)} />
        <MetadataCard label="Passed" value={String(detail.passedTests)} />
        <MetadataCard label="Failed" value={String(detail.failedTests)} />
        <MetadataCard label="Skipped" value={String(detail.skippedTests)} />
        <MetadataCard label="Duration" value={formatMs(detail.totalDurationMs)} />
        <MetadataCard label="Received" value={detail.receivedAt.slice(0, 10)} />
      </DashboardGrid>

      {slowestTests.length > 0 && (
        <ChartCard title="Slowest Tests (Top 10)">
          <DataTable columns={testCaseColumns} data={slowestTests} />
        </ChartCard>
      )}

      <div className="mt-6">
        <ChartCard title="All Test Cases">
          <StatusFilterBar current={statusFilter} onChange={setStatusFilter} />
          <DataTable columns={testCaseColumns} data={filteredCases} />
        </ChartCard>
      </div>
    </>
  );
}

function MetadataCard({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-2xl bg-white p-6 shadow-card hover:shadow-card-hover transition-shadow duration-200">
      <p className="text-[11px] font-medium uppercase tracking-[0.05em] text-slate-400">{label}</p>
      <p className="mt-2.5 text-lg font-light text-slate-700">{value}</p>
    </div>
  );
}

function StatusFilterBar({
  current,
  onChange,
}: {
  current: string;
  onChange: (v: string) => void;
}) {
  const options = ['all', 'passed', 'failed', 'skipped'];
  return (
    <div className="inline-flex rounded-xl bg-slate-100 p-1 mb-4">
      {options.map((opt) => (
        <button
          key={opt}
          onClick={() => onChange(opt)}
          className={`px-4 py-1.5 text-sm font-medium rounded-lg transition-all duration-150 ${
            current === opt
              ? 'bg-white text-slate-700 shadow-sm'
              : 'text-slate-400 hover:text-slate-600'
          }`}
        >
          {opt.charAt(0).toUpperCase() + opt.slice(1)}
        </button>
      ))}
    </div>
  );
}
