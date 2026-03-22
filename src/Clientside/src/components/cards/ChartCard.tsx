import type { ReactNode } from 'react';
import { Card, Flex, Text } from '@tremor/react';

interface ChartCardProps {
  title: string;
  subtitle?: string;
  dateRange?: string;
  onViewDetail?: () => void;
  children: ReactNode;
}

export function ChartCard({ title, subtitle, dateRange, onViewDetail, children }: ChartCardProps) {
  return (
    <Card>
      <Flex justifyContent="between" alignItems="start">
        <div>
          <Text className="font-medium text-gray-900">{title}</Text>
          {subtitle && <Text className="text-gray-500">{subtitle}</Text>}
        </div>
        <div className="flex items-center gap-3">
          {dateRange && <Text className="text-gray-400 text-xs">{dateRange}</Text>}
          {onViewDetail && (
            <button
              onClick={onViewDetail}
              className="text-sm text-blue-600 hover:text-blue-800 font-medium"
            >
              View Detail
            </button>
          )}
        </div>
      </Flex>
      <div className="mt-4">{children}</div>
    </Card>
  );
}
