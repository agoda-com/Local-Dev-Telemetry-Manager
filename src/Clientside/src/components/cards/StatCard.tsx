import type { ElementType } from 'react';
import { Card, Flex, Metric, Text, BadgeDelta, SparkAreaChart } from '@tremor/react';

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
  subtitle?: string;
  trend?: 'increase' | 'decrease' | 'unchanged';
  trendValue?: string;
  sparklineData?: { date: string; value: number }[];
}

export function StatCard({
  title,
  value,
  icon: Icon,
  subtitle,
  trend,
  trendValue,
  sparklineData,
}: StatCardProps) {
  return (
    <Card>
      <Flex alignItems="start">
        <div className="truncate">
          <Text>{title}</Text>
          <Metric className="mt-1">{value}</Metric>
        </div>
        {Icon && (
          <Icon className="h-5 w-5 text-gray-400 shrink-0" />
        )}
      </Flex>
      {(subtitle || trend) && (
        <Flex className="mt-4 space-x-2" justifyContent="start" alignItems="center">
          {trend && trendValue && (
            <BadgeDelta deltaType={TREND_MAP[trend] ?? 'unchanged'} size="xs">
              {trendValue}
            </BadgeDelta>
          )}
          {subtitle && <Text className="truncate">{subtitle}</Text>}
        </Flex>
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
    </Card>
  );
}
