import { InfoCard } from '@kentico/xperience-admin-components';
import React, { useCallback, useMemo, useState } from 'react';

import { toAdminHref } from '../shared/adminLinks';
import { downloadCsv, toRankedCsv, toTimeSeriesCsv } from '../shared/csv';
import { DataRetentionNote } from '../shared/DataRetentionNote';
import { rangeLength } from '../shared/dates';
import { formatComparison, numberFormat } from '../shared/format';
import { RankedBarChart } from '../shared/RankedBarChart';
import { RankedTable } from '../shared/RankedTable';
import { StackedColumnChart } from '../shared/StackedColumnChart';
import { StatsFilterBar } from '../shared/StatsFilterBar';
import { StatsTile } from '../shared/StatsTile';
import { toStatsSeries } from '../shared/timeSeries';
import { TimeSeriesTable } from '../shared/TimeSeriesTable';
import {
  StatsComparison,
  StatsFilter,
  StatsRankedCaptions,
  StatsRankedItem,
  StatsRankedResult,
  StatsSeries,
  StatsTimeSeriesResult,
} from '../shared/types';
import { useStatsCommand } from '../shared/useStatsCommand';
import '../shared/stats.css';

/** Mirrors `FormSubmissionsResult`. */
interface FormSubmissionsResult {
  /** One series per form with submissions (top 5, then "Other"), largest first. */
  readonly trend: StatsTimeSeriesResult;
  /** Every form, including forms with no submissions (listed last). */
  readonly forms: StatsRankedResult;
  readonly total: number;
  /** `total` compared with the previous period of the same length. */
  readonly totalComparison: StatsComparison;
  readonly formCount: number;
  readonly formsWithSubmissions: number;
  /** ISO timestamp of when the data was read from the database. */
  readonly updatedAt: string;
}

/** Mirrors `FormSubmissionsClientProperties`. */
interface FormSubmissionsTemplateProps {
  readonly report: FormSubmissionsResult;
  readonly today: string;
  /** Path of this page relative to the admin root, used to build admin links. */
  readonly pagePath: string | null;
}

const averageFormat = new Intl.NumberFormat(undefined, { maximumFractionDigits: 1 });

const personalDataErasureDocsUrl = 'https://docs.kentico.com/x/04B1CQ';

const captions: StatsRankedCaptions = {
  label: 'Form',
  value: 'Submissions',
};

const sourceHint =
  'Counts come from the form data tables, so they include every stored submission, not only submissions logged as contact activities.';

function toFilter(report: FormSubmissionsResult): StatsFilter {
  // Form data has no channel; the filter type is shared with channel reports.
  return {
    from: report.trend.from,
    to: report.trend.to,
    grouping: report.trend.grouping,
    channelId: null,
  };
}

export const FormSubmissionsTemplate = (props: FormSubmissionsTemplateProps) => {
  const { data: report, isLoading, hasError, load } =
    useStatsCommand<FormSubmissionsResult>(props.report);
  const [filter, setFilter] = useState<StatsFilter>(() => toFilter(props.report));

  const handleFilterChange = (next: StatsFilter) => {
    setFilter(next);
    void load(next);
  };

  const handleRefresh = () => {
    void load(filter, { refresh: true });
  };

  const { trend, forms } = report;
  const rangeText = `${trend.from} – ${trend.to}`;
  const days = rangeLength(trend.from, trend.to);
  const averagePerDay = days > 0 ? report.total / days : 0;
  const comparison = report.totalComparison;
  const unusedForms = forms.items.filter((item) => item.value === 0);

  const series = useMemo<StatsSeries[]>(() => toStatsSeries(trend.series), [trend.series]);

  const { pagePath } = props;
  const getAdminHref = useCallback(
    (item: StatsRankedItem) => toAdminHref(item.adminPath, pagePath),
    [pagePath],
  );

  const exportTrendCsv = () => {
    downloadCsv(
      `form-submissions_${trend.from}_${trend.to}_${trend.grouping.toLowerCase()}.csv`,
      toTimeSeriesCsv(trend.periods, series),
    );
  };

  const exportFormsCsv = () => {
    downloadCsv(
      `form-submissions-by-form_${forms.from}_${forms.to}.csv`,
      toRankedCsv(forms.items, captions, getAdminHref),
    );
  };

  const emptyMessage = 'No forms were submitted in the selected range. Try a longer range.';

  return (
    <div className="AdminStats-root">
      <StatsFilterBar
        filter={filter}
        today={props.today}
        onChange={handleFilterChange}
        onRefresh={handleRefresh}
        isLoading={isLoading}
        updatedAt={report.updatedAt}
        showChannel={false}
      />

      <div className="AdminStats-kpis">
        <InfoCard
          caption="Submissions"
          tooltip={`All form submissions in the selected range (${rangeText}). Compared with the previous period of the same length, ${comparison.previousFrom} – ${comparison.previousTo}: ${numberFormat.format(comparison.previous)} submissions. ${sourceHint}`}
          text={numberFormat.format(report.total)}
          details={formatComparison(comparison, 'submissions')}
        />
        <InfoCard
          caption="Average per day"
          tooltip="Submissions in the selected range divided by the number of days in the range."
          text={averageFormat.format(averagePerDay)}
          details={`Over ${numberFormat.format(days)} days`}
        />
        <InfoCard
          caption="Forms with no submissions"
          tooltip="Forms without any submission in the selected range. They are listed last in Forms by submissions."
          text={numberFormat.format(unusedForms.length)}
          details={unusedForms.length === 1 ? unusedForms[0].label : 'In the selected range'}
        />
      </div>

      <StatsTile
        headline="Submissions over time"
        description={`Submissions per ${trend.grouping.toLowerCase()}, stacked by form. The top 5 forms are shown, the rest are grouped as Other.`}
        isLoading={isLoading}
        hasError={hasError}
        isEmpty={report.total === 0}
        emptyMessage={emptyMessage}
        onExportCsv={exportTrendCsv}
        renderChart={() => (
          <StackedColumnChart
            periods={trend.periods}
            series={series}
            ariaLabel={`Form submissions over time, ${rangeText}`}
          />
        )}
        renderTable={() => (
          <TimeSeriesTable grouping={trend.grouping} periods={trend.periods} series={series} />
        )}
      />

      <StatsTile
        headline="Forms by submissions"
        description="Every form, most used first, including forms with no submissions. Click a form's graph bar or name in the table to open its submissions."
        isLoading={isLoading}
        hasError={hasError}
        isEmpty={report.formCount === 0}
        emptyMessage="There are no forms yet."
        onExportCsv={exportFormsCsv}
        renderChart={() => (
          <RankedBarChart
            items={forms.items}
            captions={captions}
            ariaLabel={`Forms by submissions, ${rangeText}`}
            getHref={getAdminHref}
          />
        )}
        renderTable={() => (
          <RankedTable items={forms.items} captions={captions} getAdminHref={getAdminHref} />
        )}
      />

      <DataRetentionNote
        message={
          <>
            Deleting a contact (manually or by inactive contact cleanup) deletes their activities
            but not their form submissions. Submissions are removed only when editors delete them
            or through personal data erasure. A drop in older periods can mean submissions were
            deleted, not that fewer forms were submitted.
          </>
        }
        link={{ href: personalDataErasureDocsUrl, label: 'Learn about personal data erasure' }}
      />
    </div>
  );
};
