import { InfoCard } from '@kentico/xperience-admin-components';
import React, { useState } from 'react';

import { toRankedCsv } from '../shared/csv';
import { DataRetentionNote } from '../shared/DataRetentionNote';
import { formatShare, numberFormat } from '../shared/format';
import { RankedBarChart } from '../shared/RankedBarChart';
import { RankedTable } from '../shared/RankedTable';
import { StatsFilterBar } from '../shared/StatsFilterBar';
import { StatsTile } from '../shared/StatsTile';
import {
  StatsChannelOption,
  StatsFilter,
  StatsRankedCaptions,
  StatsRankedResult,
} from '../shared/types';
import { useCsvExport } from '../shared/useCsvExport';
import { useStatsCommand } from '../shared/useStatsCommand';
import '../shared/stats.css';

/** Mirrors `TopPagesClientProperties`. */
interface TopPagesTemplateProps {
  readonly report: StatsRankedResult;
  readonly channels: readonly StatsChannelOption[];
  readonly today: string;
}

const captions: StatsRankedCaptions = {
  label: 'Page URL',
  secondaryLabel: 'Activity title',
  value: 'Visits',
  secondaryValue: 'Unique contacts',
};

function toFilter(report: StatsRankedResult): StatsFilter {
  // This report ignores grouping; the filter type is shared with trend reports.
  return { from: report.from, to: report.to, grouping: 'Day', channelId: report.channelId };
}

export const TopPagesTemplate = (props: TopPagesTemplateProps) => {
  const saveCsv = useCsvExport();
  const { data: report, isLoading, hasError, load } =
    useStatsCommand<StatsRankedResult>(props.report);
  const [filter, setFilter] = useState<StatsFilter>(() => toFilter(props.report));

  const handleFilterChange = (next: StatsFilter) => {
    setFilter(next);
    void load(next);
  };

  const handleRefresh = () => {
    void load(filter, { refresh: true });
  };

  const topPage = report.items[0];
  const rangeText = `${report.from} – ${report.to}`;
  const shownText =
    report.itemCount > report.items.length
      ? `Top ${report.items.length} of ${numberFormat.format(report.itemCount)} pages by visits.`
      : 'Pages by visits.';

  const exportCsv = () => {
    saveCsv(
      'top-pages',
      `top-pages_${report.from}_${report.to}.csv`,
      toRankedCsv(report.items, captions),
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
        showGrouping={false}
      />

      <div className="AdminStats-kpis">
        <InfoCard
          caption="Page visits"
          tooltip="All page visit activities in the selected range and channel."
          text={numberFormat.format(report.total)}
          details={rangeText}
        />
        <InfoCard
          caption="Unique pages"
          tooltip="Distinct page URLs with at least one visit. Query strings and fragments are ignored."
          text={numberFormat.format(report.itemCount)}
          details="With at least one visit"
        />
        <InfoCard
          caption="Top page"
          tooltip="Page URL with the most visits in the selected range."
          text={topPage ? topPage.label : '–'}
          details={
            topPage
              ? `${numberFormat.format(topPage.value)} visits (${formatShare(topPage.share)})`
              : 'No page visits'
          }
        />
      </div>

      <StatsTile
        headline="Top pages by visits"
        description={`${shownText} Share is of all page visits in the range.`}
        isLoading={isLoading}
        hasError={hasError}
        isEmpty={report.items.length === 0}
        emptyMessage="No page visits were logged in the selected range. Try a longer range or another channel."
        onExportCsv={exportCsv}
        renderChart={() => (
          <RankedBarChart
            items={report.items}
            captions={captions}
            ariaLabel={`Top pages by visits, ${rangeText}`}
          />
        )}
        renderTable={() => <RankedTable items={report.items} captions={captions} />}
      />

      <DataRetentionNote />
    </div>
  );
};
