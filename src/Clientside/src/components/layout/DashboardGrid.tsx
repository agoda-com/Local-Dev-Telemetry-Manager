import type { ReactNode } from 'react';

interface DashboardGridProps {
  columns: 1 | 2 | 3 | 4;
  children: ReactNode;
}

const GRID_CLASSES: Record<number, string> = {
  1: 'grid grid-cols-1 gap-5',
  2: 'grid grid-cols-1 md:grid-cols-2 gap-5',
  3: 'grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-5',
  4: 'grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-5',
};

export function DashboardGrid({ columns, children }: DashboardGridProps) {
  return <div className={`${GRID_CLASSES[columns]} mb-8`}>{children}</div>;
}
