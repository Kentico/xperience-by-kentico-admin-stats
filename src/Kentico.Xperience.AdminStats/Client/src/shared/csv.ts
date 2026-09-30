import { periodTotals } from './timeSeries';
import {
  StatsAgedItem,
  StatsCoverageItem,
  StatsPeriod,
  StatsRankedCaptions,
  StatsRankedItem,
  StatsSeries,
  StatsShareSlice,
} from './types';

export type CsvValue = string | number | null | undefined;

function escapeCell(value: CsvValue): string {
  if (value === null || value === undefined) {
    return '';
  }

  let text = String(value);

  // Prevent spreadsheet formula injection from text values.
  if (typeof value === 'string' && /^[=+\-@\t\r]/.test(text)) {
    text = `'${text}`;
  }

  return /[",\r\n]/.test(text) ? `"${text.replace(/"/g, '""')}"` : text;
}

/** Builds RFC 4180 CSV text from a header row and data rows. */
export function toCsv(
  header: readonly CsvValue[],
  rows: readonly (readonly CsvValue[])[],
): string {
  return [header, ...rows].map((row) => row.map(escapeCell).join(',')).join('\r\n');
}

/**
 * Builds CSV text for a ranked list: rank, label, optional secondary label, value,
 * optional secondary value, share (%, one decimal) and URL (the absolute URL, else the admin link
 * from `getAdminHref` made absolute).
 */
export function toRankedCsv(
  items: readonly StatsRankedItem[],
  captions: StatsRankedCaptions,
  getAdminHref?: (item: StatsRankedItem) => string | null,
): string {
  const header: CsvValue[] = [
    'Rank',
    captions.label,
    ...(captions.secondaryLabel ? [captions.secondaryLabel] : []),
    captions.value,
    ...(captions.secondaryValue ? [captions.secondaryValue] : []),
    'Share (%)',
    'URL',
  ];

  const rows = items.map((item): CsvValue[] => [
    item.rank,
    item.label,
    ...(captions.secondaryLabel ? [item.secondaryLabel] : []),
    item.value,
    ...(captions.secondaryValue ? [item.secondaryValue] : []),
    Math.round(item.share * 1000) / 10,
    item.url ?? toAbsoluteUrl(getAdminHref?.(item) ?? null),
  ]);

  return toCsv(header, rows);
}

/**
 * Builds CSV text for a time series: period start, period label, one column per series, total.
 */
export function toTimeSeriesCsv(
  periods: readonly StatsPeriod[],
  series: readonly StatsSeries[],
): string {
  const totals = periodTotals(periods, series);
  return toCsv(
    ['Period start', 'Period', ...series.map((s) => s.name), 'Total'],
    periods.map((period, index) => [
      period.start,
      period.label,
      ...series.map((s) => s.values[index] ?? 0),
      totals[index],
    ]),
  );
}

/** Builds CSV text for share slices: name, value, share (%, one decimal). */
export function toShareCsv(
  slices: readonly StatsShareSlice[],
  labelCaption: string,
  valueCaption: string,
): string {
  const total = slices.reduce((sum, s) => sum + s.value, 0);
  return toCsv(
    [labelCaption, valueCaption, 'Share (%)'],
    slices.map((slice) => [
      slice.name,
      slice.value,
      total > 0 ? Math.round((slice.value / total) * 1000) / 10 : 0,
    ]),
  );
}

/** Builds CSV text for "x of y" rows: label, optional secondary label, covered, missing, total, share (%, one decimal). */
export function toCoverageCsv(
  items: readonly StatsCoverageItem[],
  captions: {
    readonly label: string;
    readonly secondaryLabel?: string;
    readonly covered: string;
    readonly missing: string;
  },
): string {
  return toCsv(
    [
      captions.label,
      ...(captions.secondaryLabel ? [captions.secondaryLabel] : []),
      captions.covered,
      captions.missing,
      'Total',
      'Share (%)',
    ],
    items.map((item) => [
      item.label,
      ...(captions.secondaryLabel ? [item.secondaryLabel] : []),
      item.covered,
      item.missing,
      item.total,
      Math.round(item.share * 1000) / 10,
    ]),
  );
}

/**
 * Builds CSV text for an aged item list: label, optional category / language / detail, since, days
 * and URL (the admin link from `getAdminHref` made absolute).
 */
export function toAgedCsv(
  items: readonly StatsAgedItem[],
  captions: {
    readonly label: string;
    readonly category?: string;
    readonly language?: string;
    readonly detail?: string;
    readonly since: string;
    readonly days: string;
  },
  getAdminHref?: (item: StatsAgedItem) => string | null,
): string {
  return toCsv(
    [
      captions.label,
      ...(captions.category ? [captions.category] : []),
      ...(captions.language ? [captions.language] : []),
      ...(captions.detail ? [captions.detail] : []),
      captions.since,
      captions.days,
      'URL',
    ],
    items.map((item) => [
      item.label,
      ...(captions.category ? [item.category] : []),
      ...(captions.language ? [item.language] : []),
      ...(captions.detail ? [item.detail] : []),
      item.since,
      item.days,
      toAbsoluteUrl(getAdminHref?.(item) ?? null),
    ]),
  );
}

/** Starts a browser download of CSV text. Adds a BOM so Excel reads UTF-8. */
export function downloadCsv(fileName: string, csv: string): void {
  const blob = new Blob(['﻿', csv], { type: 'text/csv;charset=utf-8' });
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = fileName;
  document.body.appendChild(link);
  link.click();
  link.remove();
  URL.revokeObjectURL(url);
}

/** Makes a same-origin path absolute, so CSV links work outside the admin. */
function toAbsoluteUrl(path: string | null): string | null {
  return path ? new URL(path, window.location.origin).toString() : null;
}
