import {
  CellType,
  Link,
  LinkTableCellComponent,
  Table,
  TableCell,
  TableColumn,
  TableRow,
} from '@kentico/xperience-admin-components';
import React, { useMemo } from 'react';

import { formatItemChange, formatShare, formatValue } from './format';
import { column, stringCell } from './table';
import { StatsRankedCaptions, StatsRankedItem } from './types';

export interface RankedTableProps {
  readonly items: readonly StatsRankedItem[];
  readonly captions: StatsRankedCaptions;
  /**
   * Returns the link to the item's native admin page (same tab), or `null`.
   * Used only for items without an absolute `url`. Omit when the report has no admin links.
   */
  readonly getAdminHref?: (item: StatsRankedItem) => string | null;
  /**
   * Shows the secondary label (for example an email) in its own column after the label, captioned `captions.secondaryLabel`.
   * Default `false`: the secondary label shows only in chart tooltips and CSV.
   */
  readonly showSecondaryLabel?: boolean;
}

/**
 * Ranked list as a native admin table: rank, label (link when the item has a URL or admin link),
 * optional secondary label, value, optional secondary and third value, optional previous period value and change, and share.
 * Uses the same cell types as the other report tables (string cells, plus the admin link cell),
 * so rows keep the native single-line layout. The admin table has no column alignment option,
 * so numbers are left aligned like in other admin listings.
 */
export const RankedTable = ({ items, captions, getAdminHref, showSecondaryLabel = false }: RankedTableProps) => {
  const showSecondaryLabelColumn = showSecondaryLabel && Boolean(captions.secondaryLabel);
  const showSecondaryValue = Boolean(captions.secondaryValue);
  const showTertiaryValue = Boolean(captions.tertiaryValue);
  const showPreviousValue = Boolean(captions.previousValue);
  const showChange = Boolean(captions.change);

  const columns = useMemo<TableColumn[]>(
    () => [
      column('rank', '#', 6, 8),
      column('label', captions.label, 30, 100),
      ...(showSecondaryLabelColumn ? [column('secondaryLabel', captions.secondaryLabel ?? '', 24, 60)] : []),
      column('value', captions.value, 10, 16),
      ...(showSecondaryValue ? [column('secondaryValue', captions.secondaryValue ?? '', 12, 20)] : []),
      ...(showTertiaryValue ? [column('tertiaryValue', captions.tertiaryValue ?? '', 10, 16)] : []),
      ...(showPreviousValue ? [column('previousValue', captions.previousValue ?? '', 12, 20)] : []),
      ...(showChange ? [column('change', captions.change ?? '', 10, 14)] : []),
      column('share', 'Share', 10, 14),
    ],
    [captions, showSecondaryLabelColumn, showSecondaryValue, showTertiaryValue, showPreviousValue, showChange],
  );

  const rows = useMemo<TableRow[]>(
    () =>
      items.map((item) => ({
        identifier: item.key,
        disabled: false,
        cells: [
          stringCell('rank', String(item.rank)),
          labelCell(item, getAdminHref ? getAdminHref(item) : null),
          ...(showSecondaryLabelColumn ? [stringCell('secondaryLabel', item.secondaryLabel ?? '')] : []),
          stringCell('value', formatValue(item.value, captions.valueKind, item.valueText)),
          ...(showSecondaryValue
            ? [stringCell('secondaryValue', formatValue(item.secondaryValue, captions.secondaryValueKind, item.secondaryValueText))]
            : []),
          ...(showTertiaryValue
            ? [stringCell('tertiaryValue', formatValue(item.tertiaryValue, captions.tertiaryValueKind, item.tertiaryValueText))]
            : []),
          ...(showPreviousValue
            ? [stringCell('previousValue', formatValue(item.previousValue, captions.valueKind, item.previousValueText))]
            : []),
          ...(showChange ? [stringCell('change', formatItemChange(item))] : []),
          stringCell('share', formatShare(item.share)),
        ],
      })),
    [
      items,
      captions.valueKind,
      captions.secondaryValueKind,
      captions.tertiaryValueKind,
      showSecondaryLabelColumn,
      showSecondaryValue,
      showTertiaryValue,
      showPreviousValue,
      showChange,
      getAdminHref,
    ],
  );

  return (
    <div className="SimpleStats-tableScroll">
      <Table columns={columns} rows={rows} />
    </div>
  );
};

/**
 * Label cell:
 * - item with an absolute `url`: the admin link cell (opens in a new tab, truncates with ellipsis);
 * - item with an admin link: the admin `Link` in the same tab, wrapped like the link cell;
 * - otherwise a plain string cell.
 * The admin table renders `component` as a component type (`<component />`), so a render function is passed.
 */
function labelCell(item: StatsRankedItem, adminHref: string | null): TableCell {
  if (item.url) {
    const url = item.url;
    return {
      type: CellType.Component,
      columnName: 'label',
      component: () => <LinkTableCellComponent text={item.label} url={url} />,
    } as TableCell;
  }
  return adminLinkCell('label', item.label, adminHref);
}

/**
 * Text cell that links to a native admin page in the same tab (the admin `Link`, wrapped like the link cell),
 * or a plain string cell when there is no link.
 */
export function adminLinkCell(columnName: string, text: string, adminHref: string | null): TableCell {
  if (!adminHref) {
    return stringCell(columnName, text);
  }
  return {
    type: CellType.Component,
    columnName,
    component: () => (
      <div title={text} className="SimpleStats-cellLink">
        <Link href={adminHref} text={text} target="_self" ellipsis />
      </div>
    ),
  } as TableCell;
}
