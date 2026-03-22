import { formatNumber } from '../../utils/format';

export const CHART_HEIGHT = 'h-64';
export const Y_AXIS_WIDTH = 64;
export const CURVE_TYPE = 'monotone' as const;
export const SHOW_GRID_LINES = true;

export const defaultValueFormatter = (v: number) => formatNumber(v);
