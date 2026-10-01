import { Table, TableColumn, TableRow } from '@kentico/xperience-admin-components';
import React, { useMemo } from 'react';

import { formatShare, formatValue } from './format';
import { column, stringCell } from './table';
import { StatsShareSlice, StatsValueKind } from './types';

export interface ShareTableProps {
  readonly slices: readonly StatsShareSlice[];
  /** Caption of the name column, for example "Segment". */
  readonly labelCaption: string;
  /** Caption of the value column, for example "Contacts". */
  readonly valueCaption: string;
  /** Caption of the slices' `secondaryValue` column, for example "Revenue". Omit to hide it. */
  readonly secondaryValueCaption?: string;
  /** Format of the secondary value. Default `Count`. */
  readonly secondaryValueKind?: StatsValueKind;
}

/** Share of each slice in the total, as a native admin table: name, value, optional secondary value, share. */
export const ShareTable = ({
  slices,
  labelCaption,
  valueCaption,
  secondaryValueCaption,
  secondaryValueKind,
}: ShareTableProps) => {
  const columns = useMemo<TableColumn[]>(
    () => [
      column('label', labelCaption, 20, 40),
      column('value', valueCaption, 10, 20),
      ...(secondaryValueCaption ? [column('secondaryValue', secondaryValueCaption, 12, 20)] : []),
      column('share', 'Share', 10, 16),
    ],
    [labelCaption, valueCaption, secondaryValueCaption],
  );

  const rows = useMemo<TableRow[]>(() => {
    const total = slices.reduce((sum, s) => sum + s.value, 0);
    return slices.map((slice) => ({
      identifier: slice.key,
      disabled: false,
      cells: [
        stringCell('label', slice.name),
        stringCell('value', formatValue(slice.value)),
        ...(secondaryValueCaption
          ? [stringCell('secondaryValue', formatValue(slice.secondaryValue, secondaryValueKind, slice.secondaryValueText))]
          : []),
        stringCell('share', formatShare(total > 0 ? slice.value / total : 0)),
      ],
    }));
  }, [slices, secondaryValueCaption, secondaryValueKind]);

  return (
    <div className="SimpleStats-tableScroll">
      <Table columns={columns} rows={rows} />
    </div>
  );
};
