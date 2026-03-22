import { BarChart, type BarChartProps } from '@tremor/react';
import { CHART_HEIGHT, SHOW_GRID_LINES, defaultValueFormatter } from './chartDefaults';

type TrendBarChartProps = Omit<BarChartProps, 'className'> & {
  className?: string;
};

export function TrendBarChart({
  className,
  showGridLines = SHOW_GRID_LINES,
  valueFormatter = defaultValueFormatter,
  ...rest
}: TrendBarChartProps) {
  return (
    <BarChart
      className={className ?? CHART_HEIGHT}
      showGridLines={showGridLines}
      valueFormatter={valueFormatter}
      {...rest}
    />
  );
}
