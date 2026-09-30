import { Table, TableColumn, TableRow } from '@kentico/xperience-admin-components';
import React, { useMemo } from 'react';

import { numberFormat } from './format';
import { column, stringCell } from './table';
import { periodCaption, periodTotals } from './timeSeries';
import { StatsGrouping, StatsPeriod, StatsSeries } from './types';

export interface TimeSeriesTableProps {
  readonly grouping: StatsGrouping;
  readonly periods: readonly StatsPeriod[];
  readonly series: readonly StatsSeries[];
}

/** Period × series grid with a total column, as a native admin table. */
export const TimeSeriesTable = ({ grouping, periods, series }: TimeSeriesTableProps) => {
  const columns = useMemo<TableColumn[]>(
    () => [
      column('period', periodCaption[grouping], 14, 20),
      ...series.map((s, index) => column(`s${index}`, s.name, 10, 24)),
      column('total', 'Total', 10, 16),
    ],
    [grouping, series],
  );

  const rows = useMemo<TableRow[]>(() => {
    const totals = periodTotals(periods, series);
    return periods.map((period, index) => ({
      identifier: period.start,
      disabled: false,
      cells: [
        stringCell('period', period.label),
        ...series.map((s, seriesIndex) =>
          stringCell(`s${seriesIndex}`, numberFormat.format(s.values[index] ?? 0)),
        ),
        stringCell('total', numberFormat.format(totals[index])),
      ],
    }));
  }, [periods, series]);

  return (
    <div className="AdminStats-tableScroll">
      <Table columns={columns} rows={rows} />
    </div>
  );
};
