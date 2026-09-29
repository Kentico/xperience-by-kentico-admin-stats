import { StatsRankedCaptions, StatsRankedItem } from './types';

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
 * optional secondary value, share (%, one decimal) and URL.
 */
export function toRankedCsv(
  items: readonly StatsRankedItem[],
  captions: StatsRankedCaptions,
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
    item.url,
  ]);

  return toCsv(header, rows);
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
