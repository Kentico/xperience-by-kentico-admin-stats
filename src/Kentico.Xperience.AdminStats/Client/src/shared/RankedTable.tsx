import {
  CellType,
  LinkTableCellComponent,
  Table,
  TableCell,
  TableColumn,
  TableRow,
} from '@kentico/xperience-admin-components';
import React, { useMemo } from 'react';

import { formatShare, numberFormat } from './format';
import { column, stringCell } from './table';
import { StatsRankedCaptions, StatsRankedItem } from './types';

export interface RankedTableProps {
  readonly items: readonly StatsRankedItem[];
  readonly captions: StatsRankedCaptions;
}

/**
 * Ranked list as a native admin table: rank, label (link when the item has a URL),
 * value, optional secondary value and share.
 * Uses the same cell types as the other report tables (string cells, plus the admin link cell),
 * so rows keep the native single-line layout. The admin table has no column alignment option,
 * so numbers are left aligned like in other admin listings.
 */
export const RankedTable = ({ items, captions }: RankedTableProps) => {
  const showSecondaryValue = Boolean(captions.secondaryValue);

  const columns = useMemo<TableColumn[]>(
    () => [
      column('rank', '#', 6, 8),
      column('label', captions.label, 30, 100),
      column('value', captions.value, 10, 16),
      ...(showSecondaryValue ? [column('secondaryValue', captions.secondaryValue ?? '', 12, 20)] : []),
      column('share', 'Share', 10, 14),
    ],
    [captions, showSecondaryValue],
  );

  const rows = useMemo<TableRow[]>(
    () =>
      items.map((item) => ({
        identifier: item.key,
        disabled: false,
        cells: [
          stringCell('rank', String(item.rank)),
          labelCell(item),
          stringCell('value', numberFormat.format(item.value)),
          ...(showSecondaryValue
            ? [
                stringCell(
                  'secondaryValue',
                  item.secondaryValue === null ? '–' : numberFormat.format(item.secondaryValue),
                ),
              ]
            : []),
          stringCell('share', formatShare(item.share)),
        ],
      })),
    [items, showSecondaryValue],
  );

  return (
    <div className="AdminStats-tableScroll">
      <Table columns={columns} rows={rows} />
    </div>
  );
};

/**
 * Label as the admin link cell (opens in a new tab, truncates with ellipsis).
 * Items without a URL use a plain string cell. The admin table renders `component`
 * as a component type (`<component />`), so a render function is passed.
 */
function labelCell(item: StatsRankedItem): TableCell {
  if (!item.url) {
    return stringCell('label', item.label);
  }
  const url = item.url;
  return {
    type: CellType.Component,
    columnName: 'label',
    component: () => <LinkTableCellComponent text={item.label} url={url} />,
  } as TableCell;
}
