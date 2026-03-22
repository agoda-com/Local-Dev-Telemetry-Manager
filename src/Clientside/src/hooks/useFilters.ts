import { useState, useEffect } from 'react';
import { fetchFilters, type FilterParams, type FilterOptions } from '../api/client';
import type { FilterState } from '../components/filters/GlobalFilters';

const EMPTY_OPTIONS: FilterOptions = {
  projects: [],
  repositories: [],
  branches: [],
  testRunners: [],
  metricTypes: [],
};

const DEFAULT_FILTERS: FilterState = {
  environment: 'all',
  platforms: [],
  projectName: '',
  dateRange: '30d',
};

export function toFilterParams(state: FilterState, page?: number, pageSize?: number): FilterParams {
  return {
    environment: state.environment !== 'all' ? state.environment : undefined,
    platform: state.platforms.length > 0 ? state.platforms.join(',') : undefined,
    projectName: state.projectName || undefined,
    dateRange: state.dateRange !== 'all' ? state.dateRange : undefined,
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
      .then(opts => {
        if (!cancelled) setOptions(opts);
      })
      .catch(() => { /* filter options are non-critical */ })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => { cancelled = true; };
  }, []);

  return { filters, options, setFilters, loading };
}
