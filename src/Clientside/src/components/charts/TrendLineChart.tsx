import { LineChart, type LineChartProps } from '@tremor/react';
import {
  CHART_HEIGHT,
  Y_AXIS_WIDTH,
  CURVE_TYPE,
  SHOW_GRID_LINES,
  defaultValueFormatter,
} from './chartDefaults';

type TrendLineChartProps = Omit<LineChartProps, 'className'> & {
  className?: string;
};

export function TrendLineChart({
  className,
  yAxisWidth = Y_AXIS_WIDTH,
  curveType = CURVE_TYPE,
  showGridLines = SHOW_GRID_LINES,
  valueFormatter = defaultValueFormatter,
  ...rest
}: TrendLineChartProps) {
  return (
    <LineChart
      className={className ?? CHART_HEIGHT}
      yAxisWidth={yAxisWidth}
      curveType={curveType}
      showGridLines={showGridLines}
      valueFormatter={valueFormatter}
      {...rest}
    />
  );
}
