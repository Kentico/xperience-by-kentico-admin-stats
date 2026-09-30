import { rangeLength } from './dates';
import { StatsComparison } from './types';

export const numberFormat = new Intl.NumberFormat();

const shareFormat = new Intl.NumberFormat(undefined, {
  style: 'percent',
  minimumFractionDigits: 1,
  maximumFractionDigits: 1,
});

const changeFormat = new Intl.NumberFormat(undefined, {
  style: 'percent',
  maximumFractionDigits: 1,
  signDisplay: 'exceptZero',
});

/** Formats a 0–1 share as a percentage with one decimal. */
export function formatShare(share: number): string {
  return shareFormat.format(Number.isFinite(share) ? share : 0);
}

/** Formats a change ratio as a signed percentage, for example 0.12 as "+12%". */
export function formatChange(change: number): string {
  return changeFormat.format(Number.isFinite(change) ? change : 0);
}

/** "previous day" or "previous 30 days", for the comparison's previous period. */
export function formatPreviousPeriod(comparison: StatsComparison): string {
  const days = rangeLength(comparison.previousFrom, comparison.previousTo);
  return days === 1 ? 'previous day' : `previous ${numberFormat.format(days)} days`;
}

/**
 * Short comparison text for KPI details, for example "+12% vs previous 30 days".
 * When the previous value is 0 (no change ratio), returns "No {noun} in previous 30 days".
 */
export function formatComparison(comparison: StatsComparison, noun: string): string {
  const period = formatPreviousPeriod(comparison);
  return comparison.change === null
    ? `No ${noun} in ${period}`
    : `${formatChange(comparison.change)} vs ${period}`;
}
