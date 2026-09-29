import {
  CellType,
  ColumnContentType,
  InfoCard,
  Table,
  TableColumn,
  TableRow,
} from '@kentico/xperience-admin-components';
import React, { useMemo, useState } from 'react';

import { downloadCsv, toCsv } from '../shared/csv';
import { DataRetentionNote } from '../shared/DataRetentionNote';
import { StackedColumnChart } from '../shared/StackedColumnChart';
import { StatsFilterBar } from '../shared/StatsFilterBar';
import { StatsTile } from '../shared/StatsTile';
import {
  StatsChannelOption,
  StatsFilter,
  StatsGrouping,
  StatsPeriod,
  StatsSeries,
} from '../shared/types';
import { useStatsCommand } from '../shared/useStatsCommand';
import '../shared/stats.css';

/** Mirrors `ActivityCountsSeries`. */
interface ActivityCountsSeries {
  readonly activityType: string;
  readonly displayName: string;
  readonly values: readonly number[];
  readonly total: number;
}

/** Mirrors `ActivityCountsResult`. */
interface ActivityCountsResult {
  readonly from: string;
  readonly to: string;
  readonly grouping: StatsGrouping;
  readonly channelId: number | null;
  readonly periods: readonly StatsPeriod[];
  readonly series: readonly ActivityCountsSeries[];
  readonly total: number;
  /** ISO timestamp of when the counts were read from the database. */
  readonly updatedAt: string;
}

/** Mirrors `ActivityCountsClientProperties`. */
interface ActivityCountsTemplateProps {
  readonly report: ActivityCountsResult;
  readonly channels: readonly StatsChannelOption[];
  readonly today: string;
}

const numberFormat = new Intl.NumberFormat();

const periodCaption: Record<StatsGrouping, string> = {
  Day: 'Day',
  Week: 'Week starting',
  Month: 'Month',
};

function toFilter(report: ActivityCountsResult): StatsFilter {
  return {
    from: report.from,
    to: report.to,
    grouping: report.grouping,
    channelId: report.channelId,
  };
}

function periodTotals(report: ActivityCountsResult): number[] {
  return report.periods.map((_, index) =>
    report.series.reduce((sum, s) => sum + (s.values[index] ?? 0), 0),
  );
}

export const ActivityCountsTemplate = (props: ActivityCountsTemplateProps) => {
  const { data: report, isLoading, hasError, load } =
    useStatsCommand<ActivityCountsResult>(props.report);
  const [filter, setFilter] = useState<StatsFilter>(() => toFilter(props.report));

  const handleFilterChange = (next: StatsFilter) => {
    setFilter(next);
    void load(next);
  };

  const handleRefresh = () => {
    void load(filter, { refresh: true });
  };

  const chartSeries = useMemo<StatsSeries[]>(
    () =>
      report.series.map((s) => ({
        key: s.activityType,
        name: s.displayName,
        values: s.values,
      })),
    [report.series],
  );

  const topType = report.series[0];
  const rangeText = `${report.from} – ${report.to}`;

  const exportCsv = () => {
    const totals = periodTotals(report);
    const csv = toCsv(
      ['Period start', 'Period', ...report.series.map((s) => s.displayName), 'Total'],
      report.periods.map((period, index) => [
        period.start,
        period.label,
        ...report.series.map((s) => s.values[index] ?? 0),
        totals[index],
      ]),
    );
    downloadCsv(
      `activity-counts_${report.from}_${report.to}_${report.grouping.toLowerCase()}.csv`,
      csv,
    );
  };

  return (
    <div className="AdminStats-root">
      <StatsFilterBar
        filter={filter}
        today={props.today}
        channels={props.channels}
        onChange={handleFilterChange}
        onRefresh={handleRefresh}
        isLoading={isLoading}
        updatedAt={report.updatedAt}
      />

      <div className="AdminStats-kpis">
        <InfoCard
          caption="Total activities"
          tooltip="All logged contact activities in the selected range and channel."
          text={numberFormat.format(report.total)}
          details={rangeText}
        />
        <InfoCard
          caption="Top activity type"
          tooltip="Activity type with the most activities in the selected range."
          text={topType ? topType.displayName : '–'}
          details={
            topType
              ? `${numberFormat.format(topType.total)} activities (${Math.round(
                  (topType.total / Math.max(report.total, 1)) * 100,
                )}%)`
              : 'No activities'
          }
        />
        <InfoCard
          caption="Activity types"
          tooltip="Number of activity types with at least one activity in the selected range."
          text={numberFormat.format(report.series.length)}
          details="With at least one activity"
        />
      </div>

      <StatsTile
        headline="Activity counts by type"
        description={`Activities per ${report.grouping.toLowerCase()}, stacked by activity type.`}
        isLoading={isLoading}
        hasError={hasError}
        isEmpty={report.total === 0}
        emptyMessage="No activities were logged in the selected range. Try a longer range or another channel."
        onExportCsv={exportCsv}
        renderChart={() => (
          <StackedColumnChart
            periods={report.periods}
            series={chartSeries}
            ariaLabel={`Activity counts by type, ${rangeText}`}
          />
        )}
        renderTable={() => <ActivityCountsTable report={report} />}
      />

      <DataRetentionNote />
    </div>
  );
};

const ActivityCountsTable = ({ report }: { report: ActivityCountsResult }) => {
  const columns = useMemo<TableColumn[]>(
    () => [
      column('period', periodCaption[report.grouping], 14, 20),
      ...report.series.map((s, index) => column(`s${index}`, s.displayName, 10, 24)),
      column('total', 'Total', 10, 16),
    ],
    [report],
  );

  const rows = useMemo<TableRow[]>(() => {
    const totals = periodTotals(report);
    return report.periods.map((period, index) => ({
      identifier: period.start,
      disabled: false,
      cells: [
        stringCell('period', period.label),
        ...report.series.map((s, seriesIndex) =>
          stringCell(`s${seriesIndex}`, numberFormat.format(s.values[index] ?? 0)),
        ),
        stringCell('total', numberFormat.format(totals[index])),
      ],
    }));
  }, [report]);

  return (
    <div className="AdminStats-tableScroll">
      <Table columns={columns} rows={rows} />
    </div>
  );
};

function column(name: string, caption: string, minWidth: number, maxWidth: number): TableColumn {
  return {
    name,
    caption,
    visible: true,
    minWidth,
    maxWidth,
    contentType: ColumnContentType.Text,
    sortable: false,
    searchable: false,
  };
}

function stringCell(columnName: string, value: string) {
  return { type: CellType.String, columnName, value };
}
