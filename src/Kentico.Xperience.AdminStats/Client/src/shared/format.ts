export const numberFormat = new Intl.NumberFormat();

const shareFormat = new Intl.NumberFormat(undefined, {
  style: 'percent',
  minimumFractionDigits: 1,
  maximumFractionDigits: 1,
});

/** Formats a 0–1 share as a percentage with one decimal. */
export function formatShare(share: number): string {
  return shareFormat.format(Number.isFinite(share) ? share : 0);
}
