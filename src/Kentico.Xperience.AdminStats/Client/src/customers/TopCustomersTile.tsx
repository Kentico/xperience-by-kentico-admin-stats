import { NameToggleButton, NameToggleButtons } from '@kentico/xperience-admin-components';
import React, { useMemo, useState } from 'react';

import { RankedBarChart } from '../shared/RankedBarChart';
import { RankedTable } from '../shared/RankedTable';
import { StatsTile } from '../shared/StatsTile';
import { StatsRankedCaptions, StatsRankedItem, StatsRankedResult } from '../shared/types';

export type TopCustomersRankBy = 'revenue' | 'orders' | 'items';

const rankByItems: NameToggleButton[] = [
  { id: 'revenue', label: 'Revenue' },
  { id: 'orders', label: 'Orders' },
  { id: 'items', label: 'Items' },
];

/** Value captions per list, in the order of the server's value, secondary and third value. */
const valueCaptions: Readonly<Record<TopCustomersRankBy, readonly [string, string, string]>> = {
  revenue: ['Revenue', 'Orders', 'Items'],
  orders: ['Orders', 'Revenue', 'Items'],
  items: ['Items', 'Revenue', 'Orders'],
};

const rankByHints: Readonly<Record<TopCustomersRankBy, string>> = {
  revenue: 'Ordering customers with the most revenue (order grand totals).',
  orders: 'Ordering customers with the most orders.',
  items: 'Ordering customers with the most items bought (sum of order item quantities).',
};

export interface TopCustomersTileProps {
  readonly byRevenue: StatsRankedResult;
  readonly byOrders: StatsRankedResult;
  readonly byQuantity: StatsRankedResult;
  /** "previous 30 days", for the previous period column. */
  readonly previousPeriod: string;
  readonly isLoading: boolean;
  readonly hasError: boolean;
  readonly emptyMessage: string;
  /** Text appended to the description (change and status filter hints). */
  readonly descriptionSuffix: string;
  readonly rangeText: string;
  /** Link to the customer in the native Customers application, or `null`. */
  readonly getAdminHref: (item: StatsRankedItem) => string | null;
  /** Downloads the list; `rankBy` is the selected option. */
  readonly onExportCsv: (rankBy: TopCustomersRankBy, result: StatsRankedResult, captions: StatsRankedCaptions) => void;
}

/**
 * "Top customers" tile with a Revenue / Orders / Items "rank by" toggle. The toggle state lives here, so switching it
 * re-renders only this tile. All three lists come in the same response, so switching needs no request.
 */
export const TopCustomersTile = ({
  byRevenue,
  byOrders,
  byQuantity,
  previousPeriod,
  isLoading,
  hasError,
  emptyMessage,
  descriptionSuffix,
  rangeText,
  getAdminHref,
  onExportCsv,
}: TopCustomersTileProps) => {
  const [rankBy, setRankBy] = useState<TopCustomersRankBy>('revenue');

  const list = rankBy === 'orders' ? byOrders : rankBy === 'items' ? byQuantity : byRevenue;

  const captions = useMemo<StatsRankedCaptions>(() => {
    const [value, secondaryValue, tertiaryValue] = valueCaptions[rankBy];
    return {
      label: 'Customer',
      secondaryLabel: 'Email',
      value,
      valueKind: list.valueKind ?? 'Count',
      secondaryValue,
      secondaryValueKind: list.secondaryValueKind ?? 'Count',
      tertiaryValue,
      tertiaryValueKind: list.tertiaryValueKind ?? 'Count',
      previousValue: `${value} ${previousPeriod}`,
      change: 'Change',
    };
  }, [rankBy, list.valueKind, list.secondaryValueKind, list.tertiaryValueKind, previousPeriod]);

  return (
    <StatsTile
      headline="Top customers"
      description={`${rankByHints[rankBy]} ${descriptionSuffix}`}
      isLoading={isLoading}
      hasError={hasError}
      isEmpty={list.items.length === 0}
      emptyMessage={emptyMessage}
      onExportCsv={() => onExportCsv(rankBy, list, captions)}
      defaultView="table"
      headerControls={
        <NameToggleButtons
          items={rankByItems}
          selectedItemId={rankBy}
          onChange={(id) => setRankBy(id as TopCustomersRankBy)}
        />
      }
      renderChart={() => (
        <RankedBarChart
          items={list.items}
          captions={captions}
          ariaLabel={`Top customers by ${captions.value.toLowerCase()}, ${rangeText}`}
          getHref={getAdminHref}
        />
      )}
      renderTable={() => (
        <RankedTable items={list.items} captions={captions} getAdminHref={getAdminHref} showSecondaryLabel />
      )}
    />
  );
};
