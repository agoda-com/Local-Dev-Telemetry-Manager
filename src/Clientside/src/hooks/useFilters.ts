import { useState, useEffect } from 'react';
import { fetchFilters, type FilterParams, type FilterOptions } from '../api/client';
import type { FilterState } from '../components/filters/GlobalFilters';

const EMPTY_OPTIONS: FilterOptions = {
  projects: [],
  repositories: [],
  branches: [],
  platforms: [],
  testRunners: [],
  metricTypes: [],
};

const DEFAULT_FILTERS: FilterState = {
  environment: 'all',
  platforms: [],
  projectName: '',
  dateRange: '30d',
};

function parseDateRange(range: string): { from?: string; to?: string } {
  if (range === 'all') return {};
  const now = new Date();
  const to = now.toISOString();
  const match = /^(\d+)d$/.exec(range);
  if (!match) return {};
  const days = parseInt(match[1], 10);
  const from = new Date(now.getTime() - days * 24 * 60 * 60 * 1000).toISOString();
  return { from, to };
}

export function toFilterParams(state: FilterState, page?: number, pageSize?: number): FilterParams {
  const { from, to } = parseDateRange(state.dateRange);
  return {
    environment: state.environment !== 'all' ? state.environment : undefined,
    platform: state.platforms.length > 0 ? state.platforms.join(',') : undefined,
    project: state.projectName || undefined,
    from,
    to,
    page,
    pageSize,
  };
}

export function useFilters() {
  const [filters, setFilters] = useState<FilterState>(DEFAULT_FILTERS);
  const [options, setOptions] = useState<FilterOptions>(EMPTY_OPTIONS);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let cancelled = false;
    fetchFilters()
      .then((opts) => {
        if (!cancelled) setOptions(opts);
      })
      .catch(() => {
        /* filter options are non-critical */
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, []);

  return { filters, options, setFilters, loading };
}
