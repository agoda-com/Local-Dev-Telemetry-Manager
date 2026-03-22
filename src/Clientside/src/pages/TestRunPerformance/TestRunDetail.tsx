import { useState, useEffect, useMemo } from 'react';
import { useParams, Link } from 'react-router-dom';
import { Card, Text, Badge } from '@tremor/react';
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
      return <Text title={msg}>{msg.length > 80 ? `${msg.slice(0, 80)}…` : msg}</Text>;
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

      <Link to="/test-runs" style={{ color: '#2563eb', fontSize: '0.875rem', fontWeight: 500 }}>
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

      <ChartCard title="All Test Cases">
        <StatusFilterBar current={statusFilter} onChange={setStatusFilter} />
        <DataTable columns={testCaseColumns} data={filteredCases} />
      </ChartCard>
    </>
  );
}

function MetadataCard({ label, value }: { label: string; value: string }) {
  return (
    <Card>
      <Text>{label}</Text>
      <p style={{ fontSize: '1.125rem', fontWeight: 600, marginTop: '0.25rem' }}>{value}</p>
    </Card>
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
    <div style={{ display: 'flex', gap: '0.5rem', marginBottom: '0.75rem' }}>
      {options.map((opt) => (
        <button
          key={opt}
          onClick={() => onChange(opt)}
          style={{
            padding: '0.25rem 0.75rem',
            borderRadius: '0.375rem',
            fontSize: '0.875rem',
            fontWeight: current === opt ? 600 : 400,
            backgroundColor: current === opt ? '#2563eb' : '#f3f4f6',
            color: current === opt ? '#fff' : '#374151',
            border: 'none',
            cursor: 'pointer',
          }}
        >
          {opt.charAt(0).toUpperCase() + opt.slice(1)}
        </button>
      ))}
    </div>
  );
}
