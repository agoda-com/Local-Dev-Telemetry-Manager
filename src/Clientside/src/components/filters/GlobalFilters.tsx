import { MultiSelect, MultiSelectItem, Select, SelectItem } from '@tremor/react';
import { EnvironmentToggle } from './EnvironmentToggle';
import type { FilterOptions } from '../../api/client';

export interface FilterState {
  environment: string;
  platforms: string[];
  projectName: string;
  dateRange: string;
}

interface GlobalFiltersProps {
  filters: FilterState;
  options: FilterOptions;
  onFiltersChange: (filters: FilterState) => void;
}

export function GlobalFilters({ filters, options, onFiltersChange }: GlobalFiltersProps) {
  return (
    <div className="flex flex-wrap items-end gap-4 mb-6">
      <EnvironmentToggle
        value={filters.environment}
        onChange={(environment) => onFiltersChange({ ...filters, environment })}
      />

      <MultiSelect
        value={filters.platforms}
        onValueChange={(platforms) => onFiltersChange({ ...filters, platforms })}
        placeholder="All Platforms"
        className="max-w-xs"
      >
        {(options.metricTypes ?? []).map(p => (
          <MultiSelectItem key={p} value={p}>{p}</MultiSelectItem>
        ))}
      </MultiSelect>

      <Select
        value={filters.projectName}
        onValueChange={(projectName) => onFiltersChange({ ...filters, projectName })}
        placeholder="All Projects"
        className="max-w-xs"
      >
        {(options.projects ?? []).map(p => (
          <SelectItem key={p} value={p}>{p}</SelectItem>
        ))}
      </Select>

      <Select
        value={filters.dateRange}
        onValueChange={(dateRange) => onFiltersChange({ ...filters, dateRange })}
        placeholder="Date Range"
        className="max-w-[10rem]"
      >
        <SelectItem value="7d">Last 7 days</SelectItem>
        <SelectItem value="30d">Last 30 days</SelectItem>
        <SelectItem value="90d">Last 90 days</SelectItem>
        <SelectItem value="all">All time</SelectItem>
      </Select>
    </div>
  );
}
