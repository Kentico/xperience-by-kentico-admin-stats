import { Table, TableColumn, TableRow } from '@kentico/xperience-admin-components';
import React, { useMemo } from 'react';

import { numberFormat, signedNumberFormat } from '../shared/format';
import { adminLinkCell } from '../shared/RankedTable';
import { column, stringCell } from '../shared/table';
import type { RecipientListSummary } from './RecipientListsTemplate';

export interface RecipientListTableProps {
  readonly lists: readonly RecipientListSummary[];
  /** Link to the list in the Recipient lists application, or `null`. */
  readonly getAdminHref: (list: RecipientListSummary) => string | null;
}

/**
 * Recipient lists as a native admin table: name (link to the list), current statuses, events in the range and the net change in subscribers over the range.
 * The shared ranked table has up to three values, so this list has its own table built from the same cells.
 */
export const RecipientListTable = ({ lists, getAdminHref }: RecipientListTableProps) => {
  const columns = useMemo<TableColumn[]>(
    () => [
      column('label', 'Recipient list', 24, 60),
      column('receiving', 'Receiving', 10, 14),
      column('bounced', 'Bounced', 10, 14),
      column('unsubscribed', 'Unsubscribed', 10, 14),
      column('notConfirmed', 'Not confirmed', 10, 14),
      column('subscriptions', 'Subscriptions', 10, 14),
      column('unsubscriptions', 'Unsubscriptions', 10, 14),
      column('subscriberChange', 'Subscriber change', 12, 18),
    ],
    [],
  );

  const rows = useMemo<TableRow[]>(
    () =>
      lists.map((list) => ({
        identifier: String(list.id),
        disabled: false,
        cells: [
          adminLinkCell('label', list.displayName, getAdminHref(list)),
          stringCell('receiving', numberFormat.format(list.statuses.receiving)),
          stringCell('bounced', numberFormat.format(list.statuses.bounced)),
          stringCell('unsubscribed', numberFormat.format(list.statuses.unsubscribed)),
          stringCell('notConfirmed', numberFormat.format(list.statuses.notConfirmed)),
          stringCell('subscriptions', numberFormat.format(list.subscriptions)),
          stringCell('unsubscriptions', numberFormat.format(list.unsubscriptions)),
          stringCell('subscriberChange', signedNumberFormat.format(list.subscribers - list.previousSubscribers)),
        ],
      })),
    [lists, getAdminHref],
  );

  return (
    <div className="SimpleStats-tableScroll">
      <Table columns={columns} rows={rows} />
    </div>
  );
};
