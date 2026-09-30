import { StatsGrouping, StatsPeriod, StatsSeries, StatsTimeSeries } from './types';

/** Caption of the period column per grouping. */
export const periodCaption: Record<StatsGrouping, string> = {
  Day: 'Day',
  Week: 'Week starting',
  Month: 'Month',
};

/** Sum of all series per period. */
export function periodTotals(
  periods: readonly StatsPeriod[],
  series: readonly StatsSeries[],
): number[] {
  return periods.map((_, index) =>
    series.reduce((sum, s) => sum + (s.values[index] ?? 0), 0),
  );
}

/** Maps server time series to chart/table series. */
export function toStatsSeries(series: readonly StatsTimeSeries[]): StatsSeries[] {
  return series.map((s) => ({ key: s.key, name: s.displayName, values: s.values }));
}
