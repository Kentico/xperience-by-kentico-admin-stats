import { Table, TableColumn, TableRow } from '@kentico/xperience-admin-components';
import React, { useMemo } from 'react';

import { formatShare, numberFormat } from './format';
import { column, stringCell } from './table';
import { StatsShareSlice } from './types';

export interface ShareTableProps {
  readonly slices: readonly StatsShareSlice[];
  /** Caption of the name column, for example "Segment". */
  readonly labelCaption: string;
  /** Caption of the value column, for example "Contacts". */
  readonly valueCaption: string;
}

/** Share of each slice in the total, as a native admin table: name, value, share. */
export const ShareTable = ({ slices, labelCaption, valueCaption }: ShareTableProps) => {
  const columns = useMemo<TableColumn[]>(
    () => [
      column('label', labelCaption, 20, 40),
      column('value', valueCaption, 10, 20),
      column('share', 'Share', 10, 16),
    ],
    [labelCaption, valueCaption],
  );

  const rows = useMemo<TableRow[]>(() => {
    const total = slices.reduce((sum, s) => sum + s.value, 0);
    return slices.map((slice) => ({
      identifier: slice.key,
      disabled: false,
      cells: [
        stringCell('label', slice.name),
        stringCell('value', numberFormat.format(slice.value)),
        stringCell('share', formatShare(total > 0 ? slice.value / total : 0)),
      ],
    }));
  }, [slices]);

  return (
    <div className="AdminStats-tableScroll">
      <Table columns={columns} rows={rows} />
    </div>
  );
};
