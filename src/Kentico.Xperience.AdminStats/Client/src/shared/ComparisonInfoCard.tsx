import { InfoCard } from '@kentico/xperience-admin-components';
import React from 'react';

import { formatComparison, formatValue } from './format';
import { StatsComparison, StatsValueComparison } from './types';

export interface ComparisonInfoCardProps {
  readonly caption: string;
  /** What the value counts, for example "All events in the selected range." The comparison is appended. */
  readonly tooltip: string;
  /** A count comparison, or a decimal comparison formatted by its `kind` (for example revenue). */
  readonly comparison: StatsComparison | StatsValueComparison;
  /**
   * Plural noun for the details line, for example "errors" ("+40% vs previous 30 days", "No errors in previous 30 days").
   * Counts also use it in the tooltip ("12 errors").
   */
  readonly noun: string;
}

/**
 * KPI card with the current value and its change vs the previous period of the same length.
 * The change is shown as text (the admin `InfoCard` has no status color), so direction stays neutral.
 * A value that cannot be computed (for example an average without orders) shows "–".
 */
export const ComparisonInfoCard = ({ caption, tooltip, comparison, noun }: ComparisonInfoCardProps) => {
  const kind = 'kind' in comparison ? comparison.kind : 'Count';
  // Amounts come formatted by the project's price formatter (currency) when the server has one.
  const currentText = 'currentText' in comparison ? comparison.currentText : undefined;
  const previousText = 'previousText' in comparison ? comparison.previousText : undefined;
  const unit = kind === 'Count' ? ` ${noun}` : '';
  const details =
    comparison.current === null ? `No ${noun} in the selected range` : formatComparison(comparison, noun);

  return (
    <InfoCard
      caption={caption}
      tooltip={`${tooltip} Previous period (${comparison.previousFrom} – ${comparison.previousTo}): ${formatValue(comparison.previous, kind, previousText)}${unit}.`}
      text={formatValue(comparison.current, kind, currentText)}
      details={details}
    />
  );
};
