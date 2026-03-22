import type { ElementType } from 'react';
import { BadgeDelta, SparkAreaChart } from '@tremor/react';
import { formatNumber } from '../../utils/format';

type DeltaType = 'increase' | 'moderateIncrease' | 'unchanged' | 'moderateDecrease' | 'decrease';

const TREND_MAP: Record<string, DeltaType> = {
  increase: 'increase',
  decrease: 'decrease',
  unchanged: 'unchanged',
};

interface StatCardProps {
  title: string;
  value: string | number;
  icon?: ElementType;
  iconTint?: 'blue' | 'green' | 'amber' | 'rose';
  subtitle?: string;
  trend?: 'increase' | 'decrease' | 'unchanged';
  trendValue?: string;
  sparklineData?: { date: string; value: number }[];
}

const ICON_TINTS = {
  blue: { bg: 'bg-blue-50', text: 'text-blue-500' },
  green: { bg: 'bg-emerald-50', text: 'text-emerald-500' },
  amber: { bg: 'bg-amber-50', text: 'text-amber-500' },
  rose: { bg: 'bg-rose-50', text: 'text-rose-500' },
};

export function StatCard({
  title,
  value,
  icon: Icon,
  iconTint = 'blue',
  subtitle,
  trend,
  trendValue,
  sparklineData,
}: StatCardProps) {
  const tint = ICON_TINTS[iconTint];

  return (
    <div className="rounded-2xl bg-white p-6 shadow-card hover:shadow-card-hover transition-shadow duration-200">
      <div className="flex items-start justify-between">
        <div className="min-w-0">
          <p className="text-[13px] font-medium text-slate-400 truncate">{title}</p>
          <p className="mt-2 text-[28px] font-light tracking-tight text-slate-800">
            {typeof value === 'number' ? formatNumber(value) : value}
          </p>
        </div>
        {Icon && (
          <div className={`flex-shrink-0 rounded-xl ${tint.bg} p-2.5`}>
            <Icon className={`h-5 w-5 ${tint.text}`} />
          </div>
        )}
      </div>
      {(subtitle || trend) && (
        <div className="mt-4 flex items-center gap-2">
          {trend && trendValue && (
            <BadgeDelta deltaType={TREND_MAP[trend] ?? 'unchanged'} size="xs">
              {trendValue}
            </BadgeDelta>
          )}
          {subtitle && <p className="text-xs text-slate-400 truncate">{subtitle}</p>}
        </div>
      )}
      {sparklineData && sparklineData.length > 0 && (
        <SparkAreaChart
          data={sparklineData}
          categories={['value']}
          index="date"
          colors={['blue']}
          className="mt-4 h-10 w-full"
        />
      )}
    </div>
  );
}
