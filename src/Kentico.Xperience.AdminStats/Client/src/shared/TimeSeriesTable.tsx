import { Table, TableColumn, TableRow } from '@kentico/xperience-admin-components';
import React, { useMemo } from 'react';

import { formatValue, numberFormat } from './format';
import { column, stringCell } from './table';
import { periodCaption, periodTotals } from './timeSeries';
import { StatsGrouping, StatsPeriod, StatsSeries } from './types';

export interface TimeSeriesTableProps {
  readonly grouping: StatsGrouping;
  readonly periods: readonly StatsPeriod[];
  /** Series values are formatted by their `kind` (default count). */
  readonly series: readonly StatsSeries[];
  /**
   * Shows the total column (sum of all series). Default `true`.
   * Hide it when series measure different things (for example orders and revenue).
   */
  readonly showTotal?: boolean;
}

/** Period × series grid with an optional total column, as a native admin table. */
export const TimeSeriesTable = ({ grouping, periods, series, showTotal = true }: TimeSeriesTableProps) => {
  const columns = useMemo<TableColumn[]>(
    () => [
      column('period', periodCaption[grouping], 14, 20),
      ...series.map((s, index) => column(`s${index}`, s.name, 10, 24)),
      ...(showTotal ? [column('total', 'Total', 10, 16)] : []),
    ],
    [grouping, series, showTotal],
  );

  const rows = useMemo<TableRow[]>(() => {
    const totals = periodTotals(periods, series);
    return periods.map((period, index) => ({
      identifier: period.start,
      disabled: false,
      cells: [
        stringCell('period', period.label),
        ...series.map((s, seriesIndex) =>
          stringCell(`s${seriesIndex}`, formatValue(s.values[index] ?? 0, s.kind, s.texts?.[index])),
        ),
        ...(showTotal ? [stringCell('total', numberFormat.format(totals[index]))] : []),
      ],
    }));
  }, [periods, series, showTotal]);

  return (
    <div className="AdminStats-tableScroll">
      <Table columns={columns} rows={rows} />
    </div>
  );
};
