import { useState, useEffect } from 'react';
import {
  fetchTestRunSummary,
  fetchTestRuns,
  type TestRunSummary,
  type TestRunItem,
  type PaginatedResult,
} from '../api/client';
import type { FilterState } from '../components/filters/GlobalFilters';
import { toFilterParams } from './useFilters';

export function useTestRunSummary(filters: FilterState) {
  const [summary, setSummary] = useState<TestRunSummary | null>(null);
  const [runs, setRuns] = useState<PaginatedResult<TestRunItem> | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<Error | null>(null);
  const [page, setPage] = useState(1);

  const filterKey = `${filters.environment}|${filters.platforms.join(',')}|${filters.projectName}|${filters.dateRange}`;

  useEffect(() => {
    let cancelled = false;
    const params = toFilterParams(filters, page, 20);
    setLoading(true);
    setError(null);

    Promise.all([fetchTestRunSummary(params), fetchTestRuns(params)])
      .then(([s, r]) => {
        if (!cancelled) {
          setSummary(s);
          setRuns(r);
        }
      })
      .catch((e) => {
        if (!cancelled) {
          setError(e instanceof Error ? e : new Error(String(e)));
        }
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [filterKey, page]); // eslint-disable-line react-hooks/exhaustive-deps

  return { summary, runs, loading, error, page, setPage };
}
