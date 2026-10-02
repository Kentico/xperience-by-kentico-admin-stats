import { ButtonColor, LinkButton } from '@kentico/xperience-admin-components';
import React, { useCallback, useMemo, useState } from 'react';

import { toAdminHref } from '../shared/adminLinks';
import { ComparisonInfoCard } from '../shared/ComparisonInfoCard';
import { toAbsoluteUrl, toCsv, toTimeSeriesCsv } from '../shared/csv';
import { DataRetentionNote } from '../shared/DataRetentionNote';
import { RankedBarChart } from '../shared/RankedBarChart';
import { StackedColumnChart } from '../shared/StackedColumnChart';
import { StatsFilterBar } from '../shared/StatsFilterBar';
import { StatsTile } from '../shared/StatsTile';
import { toValueSeries } from '../shared/timeSeries';
import { TimeSeriesTable } from '../shared/TimeSeriesTable';
import {
  StatsChannelOption,
  StatsFilter,
  StatsGrouping,
  StatsPeriod,
  StatsRankedCaptions,
  StatsRankedItem,
  StatsValueComparison,
  StatsValueSeries,
} from '../shared/types';
import { useCsvExport } from '../shared/useCsvExport';
import { useStatsCommand } from '../shared/useStatsCommand';
import { AutomatedEmailTable, EmailTable, formatPurpose, formatSendTime } from './EmailTables';
import { EmailSummaryPerformers, PerformersTile } from './PerformersTile';
import '../shared/stats.css';

/** Mirrors `EmailStatisticsValues`: lifetime numbers of an email. Bounces and spam are `null` when not tracked. */
export interface EmailStatisticsValues {
  readonly sent: number;
  readonly delivered: number;
  readonly uniqueOpens: number;
  readonly uniqueClicks: number;
  readonly softBounces: number | null;
  readonly hardBounces: number | null;
  readonly unsubscribes: number;
  readonly spamReports: number | null;
}

/** Mirrors `EmailRates`. Ratios, `null` when the denominator is 0; can be over 1. */
export interface EmailRates {
  readonly deliveryRate: number | null;
  readonly openRate: number | null;
  readonly clickRate: number | null;
  readonly unsubscribeRate: number | null;
}

/** Mirrors `EmailSummaryEmail`. */
export interface EmailSummaryEmail {
  readonly id: number;
  readonly name: string;
  /** Server local time, `yyyy-MM-ddTHH:mm:ss`. */
  readonly sendTime: string;
  readonly recipientList: string | null;
  readonly statistics: EmailStatisticsValues;
  readonly hasStatistics: boolean;
  readonly rates: EmailRates;
  /** Statistics tab, relative to the admin root. */
  readonly statisticsPath: string | null;
}

/** Mirrors `EmailSummaryAutomatedEmail`. */
export interface EmailSummaryAutomatedEmail {
  readonly id: number;
  readonly name: string;
  readonly purpose: string;
  readonly sentInRange: number;
  readonly statistics: EmailStatisticsValues;
  readonly rates: EmailRates;
  readonly statisticsPath: string | null;
}

/** Mirrors `EmailSummaryTotals`. */
interface EmailSummaryTotals {
  readonly emails: StatsValueComparison;
  readonly sent: StatsValueComparison;
  readonly delivered: StatsValueComparison;
  readonly deliveryRate: StatsValueComparison;
  readonly openRate: StatsValueComparison;
  readonly clickRate: StatsValueComparison;
  readonly hardBounces: StatsValueComparison;
  readonly softBounces: StatsValueComparison;
  readonly unsubscribes: StatsValueComparison;
  readonly unsubscribeRate: StatsValueComparison;
  readonly spamReports: StatsValueComparison;
}

/** Mirrors `EmailSummaryResult`. */
interface EmailSummaryResult {
  readonly from: string;
  readonly to: string;
  readonly grouping: StatsGrouping;
  readonly channelId: number | null;
  readonly periods: readonly StatsPeriod[];
  readonly totals: EmailSummaryTotals;
  readonly sent: StatsValueSeries;
  readonly uniqueOpens: StatsValueSeries;
  readonly uniqueClicks: StatsValueSeries;
  readonly unsubscribes: StatsValueSeries;
  /** Regular emails sent in the range, newest first. */
  readonly emails: readonly EmailSummaryEmail[];
  /** Automated emails with statistics (lifetime). */
  readonly automated: readonly EmailSummaryAutomatedEmail[];
  readonly byOpenRate: EmailSummaryPerformers;
  readonly byClickRate: EmailSummaryPerformers;
  readonly available: boolean;
  readonly minDeliveredForRanking: number;
  /** Native email list of the selected (or only) email channel, relative to the admin root. */
  readonly emailsAppPath: string | null;
  readonly updatedAt: string;
}

/** Mirrors `EmailSummaryClientProperties`. */
interface EmailSummaryTemplateProps {
  readonly report: EmailSummaryResult;
  /** Email channels. */
  readonly channels: readonly StatsChannelOption[];
  readonly today: string;
  readonly pagePath: string | null;
}

const emailStatisticsDocsUrl = 'https://docs.kentico.com/documentation/business-users/digital-marketing/emails/track-email-statistics';

const scopeHint =
  'Regular emails with a send date in the selected range (sent or sending). Numbers are lifetime totals of these emails, the same as their Statistics tab, so opens and clicks after the range still count.';

const rateHint =
  'Rates follow the Statistics tab: open and click rate = unique opens or clicks divided by delivered. The email list divides by sent, so it can show a slightly lower rate. A click counts as an open, so rates can be over 100% when recipients open or click without a recorded send.';

const sumHint = 'Range rates are sums divided by sums (for example all unique opens divided by all delivered), not averages of the email rates.';

const activityHint =
  'Email activity of all emails (regular and automated) by when recipients acted, not by send date. Sent = emails sent. Unique opens, clicks and unsubscribes = recipients with at least one in the period (a click counts as an open); a recipient who acts in two periods counts in both.';

const emailCaptions: StatsRankedCaptions = { label: 'Email', secondaryLabel: 'Send date', value: 'Open rate', valueKind: 'Ratio' };

function toFilter(report: EmailSummaryResult): StatsFilter {
  return { from: report.from, to: report.to, grouping: report.grouping, channelId: report.channelId };
}

export const EmailSummaryTemplate = (props: EmailSummaryTemplateProps) => {
  const saveCsv = useCsvExport();
  const { data: report, isLoading, hasError, load } = useStatsCommand<EmailSummaryResult>(props.report);
  const [filter, setFilter] = useState<StatsFilter>(() => toFilter(props.report));

  const handleFilterChange = (next: StatsFilter) => {
    setFilter(next);
    void load(next);
  };

  const handleRefresh = () => {
    void load(filter, { refresh: true });
  };

  const { totals, periods, emails, automated } = report;
  const { pagePath } = props;
  const appHref = toAdminHref(report.emailsAppPath, pagePath);
  const rangeText = `${report.from} – ${report.to}`;
  const period = report.grouping.toLowerCase();
  const channelSuffix = report.channelId === null ? '' : `_channel-${report.channelId}`;
  const fileSuffix = `${channelSuffix}_${report.from}_${report.to}`;
  const showHardBounces = emails.some((e) => e.statistics.hardBounces !== null);
  const showHardBouncesKpi = totals.hardBounces.current !== null || totals.hardBounces.previous !== null;
  const showSpamKpi = totals.spamReports.current !== null || totals.spamReports.previous !== null;

  const activitySeries = useMemo(
    () => [
      toValueSeries(report.sent),
      toValueSeries(report.uniqueOpens),
      toValueSeries(report.uniqueClicks),
      toValueSeries(report.unsubscribes),
    ],
    [report.sent, report.uniqueOpens, report.uniqueClicks, report.unsubscribes],
  );
  const hasActivity = activitySeries.some((s) => s.values.some((v) => v > 0));

  const getHref = useCallback((path: string | null) => toAdminHref(path, pagePath), [pagePath]);
  const getItemHref = useCallback((item: StatsRankedItem) => toAdminHref(item.adminPath, pagePath), [pagePath]);

  // The bar chart reuses the shared ranked chart: open rate per email, newest first.
  const emailItems = useMemo<StatsRankedItem[]>(
    () =>
      emails.map((email, index) => ({
        rank: index + 1,
        key: String(email.id),
        label: email.name,
        secondaryLabel: formatSendTime(email.sendTime),
        value: email.rates.openRate ?? 0,
        secondaryValue: null,
        share: 0,
        url: null,
        adminPath: email.statisticsPath,
      })),
    [emails],
  );

  const noTables = 'Email tables were not found in this project, so there are no emails to show.';
  const noEmails = report.available
    ? 'No regular email was sent in the selected range. Try a longer range.'
    : noTables;

  const exportActivityCsv = () => {
    saveCsv(
      'email-summary-activity',
      `email-summary-activity${fileSuffix}_${period}.csv`,
      toTimeSeriesCsv(periods, activitySeries, { includeTotal: false }),
    );
  };

  const exportEmailsCsv = () => {
    saveCsv(
      'email-summary-emails',
      `email-summary-emails${fileSuffix}.csv`,
      toCsv(
        [
          'Email',
          'Send date',
          'Recipient list',
          'Sent',
          'Delivered',
          'Unique opens',
          'Unique clicks',
          'Delivery rate (%)',
          'Open rate (%)',
          'Click rate (%)',
          'Soft bounces',
          'Hard bounces',
          'Unsubscribes',
          'Unsubscribe rate (%)',
          'Spam reports',
          'URL',
        ],
        emails.map((email) => [
          email.name,
          formatSendTime(email.sendTime),
          email.recipientList,
          email.statistics.sent,
          email.statistics.delivered,
          email.statistics.uniqueOpens,
          email.statistics.uniqueClicks,
          toPercent(email.rates.deliveryRate),
          toPercent(email.rates.openRate),
          toPercent(email.rates.clickRate),
          email.statistics.softBounces,
          email.statistics.hardBounces,
          email.statistics.unsubscribes,
          toPercent(email.rates.unsubscribeRate),
          email.statistics.spamReports,
          toAbsoluteUrl(getHref(email.statisticsPath)),
        ]),
      ),
    );
  };

  const exportPerformersCsv = (rankBy: string, performers: EmailSummaryPerformers, captions: StatsRankedCaptions) => {
    const rows = [
      ...performers.top.items.map((item) => ['Top', item] as const),
      ...performers.bottom.items.map((item) => ['Bottom', item] as const),
    ];
    saveCsv(
      'email-summary-performers',
      `email-summary-performers${fileSuffix}_${rankBy}-rate.csv`,
      toCsv(
        ['List', 'Rank', 'Email', 'Send date', `${captions.value} (%)`, 'Delivered', 'URL'],
        rows.map(([list, item]) => [
          list,
          item.rank,
          item.label,
          item.secondaryLabel,
          toPercent(item.value),
          item.secondaryValue,
          toAbsoluteUrl(getItemHref(item)),
        ]),
      ),
    );
  };

  const exportAutomatedCsv = () => {
    saveCsv(
      'email-summary-automated',
      `email-summary-automated${fileSuffix}.csv`,
      toCsv(
        ['Email', 'Purpose', 'Sent in range', 'Sent (lifetime)', 'Delivered', 'Unique opens', 'Unique clicks', 'Open rate (%)', 'Click rate (%)', 'URL'],
        automated.map((email) => [
          email.name,
          formatPurpose(email.purpose),
          email.sentInRange,
          email.statistics.sent,
          email.statistics.delivered,
          email.statistics.uniqueOpens,
          email.statistics.uniqueClicks,
          toPercent(email.rates.openRate),
          toPercent(email.rates.clickRate),
          toAbsoluteUrl(getHref(email.statisticsPath)),
        ]),
      ),
    );
  };

  return (
    <div className="SimpleStats-root">
      <StatsFilterBar
        filter={filter}
        today={props.today}
        channels={props.channels.length > 1 ? props.channels : []}
        onChange={handleFilterChange}
        onRefresh={handleRefresh}
        isLoading={isLoading}
        updatedAt={report.updatedAt}
        actions={
          appHref && (
            <LinkButton label="Open emails" color={ButtonColor.Tertiary} href={appHref} title="Open the email list of the email channel" />
          )
        }
      />

      <div className="SimpleStats-kpis">
        <ComparisonInfoCard
          caption="Emails sent"
          tooltip={`Regular emails with a send date in the selected range (${rangeText}).`}
          comparison={totals.emails}
          noun="emails"
        />
        <ComparisonInfoCard
          caption="Sent"
          tooltip={`Sent emails (recipients) of the regular emails sent in the selected range. ${scopeHint}`}
          comparison={totals.sent}
          noun="sent emails"
        />
        <ComparisonInfoCard
          caption="Open rate"
          tooltip={`Unique opens divided by delivered, for the regular emails sent in the selected range. ${sumHint} The change is in percentage points (pp).`}
          comparison={totals.openRate}
          noun="delivered emails"
        />
        <ComparisonInfoCard
          caption="Click rate"
          tooltip={`Unique clicks divided by delivered, for the regular emails sent in the selected range. ${sumHint} The change is in percentage points (pp).`}
          comparison={totals.clickRate}
          noun="delivered emails"
        />
      </div>

      <div className="SimpleStats-kpis">
        <ComparisonInfoCard
          caption="Delivery rate"
          tooltip="Delivered divided by sent, for the regular emails sent in the selected range. Delivered depends on the delivery provider. The change is in percentage points (pp)."
          comparison={totals.deliveryRate}
          noun="sent emails"
        />
        {showHardBouncesKpi && (
          <ComparisonInfoCard
            caption="Hard bounces"
            tooltip="Hard bounces of the regular emails sent in the selected range, from the email statistics (set by the delivery provider). Emails without bounce tracking are left out."
            comparison={totals.hardBounces}
            noun="hard bounces"
          />
        )}
        <ComparisonInfoCard
          caption="Unsubscribe rate"
          tooltip="Recipients who unsubscribed through the email, divided by sent, for the regular emails sent in the selected range. The change is in percentage points (pp)."
          comparison={totals.unsubscribeRate}
          noun="sent emails"
        />
        {showSpamKpi && (
          <ComparisonInfoCard
            caption="Spam reports"
            tooltip="Spam reports of the regular emails sent in the selected range. Only some delivery providers (for example SendGrid) report them; emails without spam tracking are left out."
            comparison={totals.spamReports}
            noun="spam reports"
          />
        )}
      </div>

      <StatsTile
        headline="Email activity over time"
        description={`${activityHint} Per ${period}.`}
        isLoading={isLoading}
        hasError={hasError}
        isEmpty={!hasActivity}
        emptyMessage={report.available ? 'No email activity in the selected range.' : noTables}
        onExportCsv={exportActivityCsv}
        renderChart={() => (
          <StackedColumnChart periods={periods} series={activitySeries} stacked={false} ariaLabel={`Email activity, ${rangeText}`} />
        )}
        renderTable={() => <TimeSeriesTable grouping={report.grouping} periods={periods} series={activitySeries} showTotal={false} />}
      />

      <StatsTile
        headline="Emails sent in range"
        description={`${scopeHint} ${rateHint} Bounces and spam reports depend on the delivery provider ("–" when not tracked).`}
        isLoading={isLoading}
        hasError={hasError}
        isEmpty={emails.length === 0}
        emptyMessage={noEmails}
        onExportCsv={exportEmailsCsv}
        defaultView="table"
        renderChart={() => (
          <RankedBarChart
            items={emailItems}
            captions={emailCaptions}
            getHref={getItemHref}
            showShare={false}
            ariaLabel={`Open rate per email, ${rangeText}`}
          />
        )}
        renderTable={() => <EmailTable emails={emails} showHardBounces={showHardBounces} getAdminHref={getHref} />}
      />

      <div className="SimpleStats-tiles SimpleStats-tiles--halves">
        <PerformersTile
          byOpenRate={report.byOpenRate}
          byClickRate={report.byClickRate}
          minDelivered={report.minDeliveredForRanking}
          isLoading={isLoading}
          hasError={hasError}
          emptyMessage={
            emails.length === 0
              ? noEmails
              : `No email sent in the selected range has at least ${report.minDeliveredForRanking} delivered.`
          }
          rangeText={rangeText}
          getAdminHref={getItemHref}
          onExportCsv={exportPerformersCsv}
        />

        <StatsTile
          headline="Automated emails"
          description="Automation emails, form autoresponders, confirmations and other emails that are not regular emails, with statistics. These are lifetime numbers, not limited to the range; only Sent in range counts sends in the selected range."
          isLoading={isLoading}
          hasError={hasError}
          isEmpty={automated.length === 0}
          emptyMessage={report.available ? 'No automated email has statistics yet.' : noTables}
          onExportCsv={exportAutomatedCsv}
          defaultView="table"
          renderChart={() => (
            <RankedBarChart
              items={automated.map((email, index) => ({
                rank: index + 1,
                key: String(email.id),
                label: email.name,
                secondaryLabel: formatPurpose(email.purpose),
                value: email.statistics.sent,
                secondaryValue: null,
                share: 0,
                url: null,
                adminPath: email.statisticsPath,
              }))}
              captions={{ label: 'Email', secondaryLabel: 'Purpose', value: 'Sent (lifetime)' }}
              getHref={getItemHref}
              showShare={false}
              ariaLabel="Automated emails by sent, lifetime"
            />
          )}
          renderTable={() => <AutomatedEmailTable emails={automated} getAdminHref={getHref} />}
        />
      </div>

      <DataRetentionNote
        message="Email numbers come from the email statistics, which are recalculated by a scheduled task (or Refresh on the Statistics tab), so the newest opens and clicks can be missing. Bounces, delivered and spam reports depend on the delivery provider. Deleting an email deletes its statistics."
        link={{ href: emailStatisticsDocsUrl, label: 'Learn about email statistics' }}
      />
    </div>
  );
};

/** Ratio as a percentage with one decimal for CSV, empty when `null`. */
function toPercent(value: number | null | undefined): number | null {
  return value === null || value === undefined ? null : Math.round(value * 1000) / 10;
}
