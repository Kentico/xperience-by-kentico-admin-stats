/** Parses a `yyyy-MM-dd` string as a local date (no time zone shift). */
export function parseDateOnly(value: string): Date {
  const [year, month, day] = value.split('-').map(Number);
  return new Date(year, month - 1, day);
}

/** Formats a local date as `yyyy-MM-dd`. */
export function formatDateOnly(date: Date): string {
  const y = date.getFullYear();
  const m = String(date.getMonth() + 1).padStart(2, '0');
  const d = String(date.getDate()).padStart(2, '0');
  return `${y}-${m}-${d}`;
}

/** Adds days to a `yyyy-MM-dd` string. */
export function addDays(value: string, days: number): string {
  const date = parseDateOnly(value);
  date.setDate(date.getDate() + days);
  return formatDateOnly(date);
}

/** Inclusive number of days between two `yyyy-MM-dd` strings. */
export function rangeLength(from: string, to: string): number {
  const ms = parseDateOnly(to).getTime() - parseDateOnly(from).getTime();
  return Math.round(ms / 86_400_000) + 1;
}
