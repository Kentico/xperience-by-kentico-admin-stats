import { Table, TableColumn, TableRow } from '@kentico/xperience-admin-components';
import React, { useMemo } from 'react';

import { CoverageCaptions } from './CoverageBarChart';
import { formatShare, numberFormat } from './format';
import { column, stringCell } from './table';
import { StatsCoverageItem } from './types';

export interface CoverageTableProps {
  readonly items: readonly StatsCoverageItem[];
  /** Caption of the label column, for example "Language". */
  readonly labelCaption: string;
  readonly captions: CoverageCaptions;
}

/** "x of y" rows as a native admin table: label, covered, missing, share. */
export const CoverageTable = ({ items, labelCaption, captions }: CoverageTableProps) => {
  const columns = useMemo<TableColumn[]>(
    () => [
      column('label', labelCaption, 20, 40),
      column('covered', captions.covered, 10, 20),
      column('missing', captions.missing, 10, 20),
      column('share', 'Share', 10, 16),
    ],
    [labelCaption, captions],
  );

  const rows = useMemo<TableRow[]>(
    () =>
      items.map((item) => ({
        identifier: item.key,
        disabled: false,
        cells: [
          stringCell('label', item.label),
          stringCell('covered', numberFormat.format(item.covered)),
          stringCell('missing', numberFormat.format(item.missing)),
          stringCell('share', formatShare(item.share)),
        ],
      })),
    [items],
  );

  return (
    <div className="SimpleStats-tableScroll">
      <Table columns={columns} rows={rows} />
    </div>
  );
};
