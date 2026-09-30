import { InfoCard } from '@kentico/xperience-admin-components';
import React from 'react';

import { formatComparison, numberFormat } from './format';
import { StatsComparison } from './types';

export interface ComparisonInfoCardProps {
  readonly caption: string;
  /** What the value counts, for example "All events in the selected range." The comparison is appended. */
  readonly tooltip: string;
  readonly comparison: StatsComparison;
  /** Plural noun for the details line, for example "errors" ("+40% vs previous 30 days", "No errors in previous 30 days"). */
  readonly noun: string;
}

/**
 * KPI card with the current value and its change vs the previous period of the same length.
 * The change is shown as text (the admin `InfoCard` has no status color), so direction stays neutral.
 */
export const ComparisonInfoCard = ({ caption, tooltip, comparison, noun }: ComparisonInfoCardProps) => (
  <InfoCard
    caption={caption}
    tooltip={`${tooltip} Previous period (${comparison.previousFrom} – ${comparison.previousTo}): ${numberFormat.format(comparison.previous)} ${noun}.`}
    text={numberFormat.format(comparison.current)}
    details={formatComparison(comparison, noun)}
  />
);
