import { rangeLength } from './dates';
import { StatsValueKind } from './types';

export const numberFormat = new Intl.NumberFormat();

/** Amounts: 2 decimals, locale grouping, no currency symbol (orders store no currency). */
const amountFormat = new Intl.NumberFormat(undefined, {
  minimumFractionDigits: 2,
  maximumFractionDigits: 2,
});

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

/** Formats a money amount with 2 decimals and locale grouping, without a currency symbol. */
export function formatAmount(value: number): string {
  return amountFormat.format(Number.isFinite(value) ? value : 0);
}

/**
 * Formats a value by what it measures: counts with locale grouping, amounts with 2 decimals,
 * ratios as percentages. `null` (a value that cannot be computed) is "–".
 * A server `text` (the amount formatted by the project's price formatter, with its currency) wins when given.
 */
export function formatValue(
  value: number | null | undefined,
  kind: StatsValueKind = 'Count',
  text?: string | null,
): string {
  if (text) {
    return text;
  }
  if (value === null || value === undefined || !Number.isFinite(value)) {
    return '–';
  }
  switch (kind) {
    case 'Amount':
      return amountFormat.format(value);
    case 'Ratio':
      return shareFormat.format(value);
    default:
      return numberFormat.format(value);
  }
}

/**
 * amCharts number format for a value kind (axis labels and `{value}` placeholders).
 * Axis ticks use plain numbers (amounts too: the project's price formatter runs on the server only,
 * so tooltips and tables show the formatted amounts); decimals show only when values have them.
 */
export function chartNumberFormat(kind: StatsValueKind = 'Count'): string {
  return kind === 'Ratio' ? '#.#%' : '#,###.##';
}

/** Formats a change ratio as a signed percentage, for example 0.12 as "+12%". */
export function formatChange(change: number): string {
  return changeFormat.format(Number.isFinite(change) ? change : 0);
}

/** The previous period of a comparison. */
interface PreviousPeriod {
  readonly previousFrom: string;
  readonly previousTo: string;
}

/** "previous day" or "previous 30 days", for the comparison's previous period. */
export function formatPreviousPeriod(comparison: PreviousPeriod): string {
  const days = rangeLength(comparison.previousFrom, comparison.previousTo);
  return days === 1 ? 'previous day' : `previous ${numberFormat.format(days)} days`;
}

/**
 * Short comparison text for KPI details, for example "+12% vs previous 30 days".
 * When there is no change ratio (the previous value is 0 or cannot be computed), returns "No {noun} in previous 30 days".
 */
export function formatComparison(
  comparison: PreviousPeriod & { readonly change: number | null },
  noun: string,
): string {
  const period = formatPreviousPeriod(comparison);
  return comparison.change === null
    ? `No ${noun} in ${period}`
    : `${formatChange(comparison.change)} vs ${period}`;
}

/**
 * Change of a ranked item vs the previous period, for example "+12%".
 * "New" when the item had no events in the previous period, "–" when the item has no comparison.
 */
export function formatItemChange(item: {
  readonly value: number;
  readonly previousValue?: number | null;
  readonly change?: number | null;
}): string {
  if (item.change !== null && item.change !== undefined) {
    return formatChange(item.change);
  }
  return item.previousValue === 0 && item.value > 0 ? 'New' : '–';
}
