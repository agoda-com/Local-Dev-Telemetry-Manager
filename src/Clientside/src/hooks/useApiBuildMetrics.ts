import { useState, useEffect } from 'react';
import {
  fetchApiBuildSummary,
  fetchBuildMetrics,
  type ApiBuildSummary,
  type BuildMetricItem,
  type PaginatedResult,
} from '../api/client';
import type { FilterState } from '../components/filters/GlobalFilters';
import { toFilterParams } from './useFilters';

export function useApiBuildMetrics(filters: FilterState) {
  const [data, setData] = useState<ApiBuildSummary | null>(null);
  const [builds, setBuilds] = useState<PaginatedResult<BuildMetricItem> | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<Error | null>(null);
  const [page, setPage] = useState(1);

  const filterKey = `${filters.environment}|${filters.platforms.join(',')}|${filters.projectName}|${filters.dateRange}`;

  useEffect(() => {
    let cancelled = false;
    const params = toFilterParams(filters, page, 20);
    setLoading(true);
    setError(null);

    Promise.all([fetchApiBuildSummary(params), fetchBuildMetrics({ ...params, buildCategory: 'API' })])
      .then(([summary, b]) => {
        if (!cancelled) {
          setData(summary);
          setBuilds(b);
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

  return { data, builds, loading, error, page, setPage };
}
