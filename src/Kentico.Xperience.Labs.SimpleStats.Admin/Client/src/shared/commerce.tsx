import React from 'react';

import { IdSelect } from './filterControls';
import { StatsSeries } from './types';

/** Mirrors `CommerceOrderStatusOption`: an order status of the project, in status order. */
export interface CommerceOrderStatusOption {
  readonly id: number;
  readonly displayName: string;
}

/** Product docs of digital commerce, linked from the data notes of the commerce reports. */
export const commerceDocsUrl = 'https://docs.kentico.com/documentation/business-users/manage-commerce-stores';

/** CSV headers of amount columns: values are raw numbers (dot decimal, no currency), not the formatted amounts. */
export const rawAmountNote = '(raw amount)';

/** Marks the CSV header of an amount series with `rawAmountNote`. */
export function toRawCsvSeries(series: StatsSeries): StatsSeries {
  return series.kind === 'Amount' ? { ...series, name: `${series.name} ${rawAmountNote}` } : series;
}

export interface OrderStatusSelectProps {
  /** The project's order statuses. The select is hidden when there are none (commerce not used). */
  readonly statuses: readonly CommerceOrderStatusOption[];
  /** Selected status ID, `null` for all statuses. */
  readonly value: number | null;
  readonly onChange: (orderStatusId: number | null) => void;
}

/** "Order status" filter item of the commerce reports: "All statuses" plus the project's statuses. */
export const OrderStatusSelect = ({ statuses, value, onChange }: OrderStatusSelectProps) =>
  statuses.length > 0 ? (
    <IdSelect
      label="Order status"
      allLabel="All statuses"
      options={statuses.map((s) => ({ id: s.id, label: s.displayName }))}
      value={value}
      onChange={onChange}
    />
  ) : null;
