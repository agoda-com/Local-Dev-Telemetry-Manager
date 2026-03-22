import type { ReactNode } from 'react';

interface ChartCardProps {
  title: string;
  subtitle?: string;
  dateRange?: string;
  onViewDetail?: () => void;
  children: ReactNode;
}

export function ChartCard({ title, subtitle, dateRange, onViewDetail, children }: ChartCardProps) {
  return (
    <div className="rounded-2xl bg-white p-6 shadow-card hover:shadow-card-hover transition-shadow duration-200">
      <div className="flex items-start justify-between mb-6">
        <div>
          <h3 className="text-[15px] font-medium text-slate-700">{title}</h3>
          {subtitle && <p className="mt-0.5 text-xs text-slate-400">{subtitle}</p>}
        </div>
        <div className="flex items-center gap-3">
          {dateRange && <span className="text-xs text-slate-400">{dateRange}</span>}
          {onViewDetail && (
            <button
              onClick={onViewDetail}
              className="text-sm font-medium text-brand-500 hover:text-brand-600 transition-colors rounded-lg px-3 py-1.5 hover:bg-brand-50"
            >
              View Detail
            </button>
          )}
        </div>
      </div>
      <div>{children}</div>
    </div>
  );
}
