import { ButtonColor, LinkButton } from '@kentico/xperience-admin-components';
import React, { useCallback, useMemo, useState } from 'react';

import { toAdminHref } from '../shared/adminLinks';
import { ComboChart } from '../shared/ComboChart';
import { ComparisonInfoCard } from '../shared/ComparisonInfoCard';
import { toAbsoluteUrl, toCsv, toShareCsv, toTimeSeriesCsv } from '../shared/csv';
import { DataRetentionNote } from '../shared/DataRetentionNote';
import { DonutChart } from '../shared/DonutChart';
import { IdSelect } from '../shared/filterControls';
import { formatShare } from '../shared/format';
import { RankedBarChart } from '../shared/RankedBarChart';
import { ShareTable } from '../shared/ShareTable';
import { StackedColumnChart } from '../shared/StackedColumnChart';
import { StatsFilterBar } from '../shared/StatsFilterBar';
import { StatsTile } from '../shared/StatsTile';
import { toValueSeries } from '../shared/timeSeries';
import { TimeSeriesTable } from '../shared/TimeSeriesTable';
import {
  StatsFilter,
  StatsGrouping,
  StatsPeriod,
  StatsRankedCaptions,
  StatsRankedItem,
  StatsShareSlice,
  StatsValueComparison,
  StatsValueSeries,
} from '../shared/types';
import { useCsvExport } from '../shared/useCsvExport';
import { useStatsCommand } from '../shared/useStatsCommand';
import { RecipientListTable } from './RecipientListTable';
import '../shared/stats.css';

/** Mirrors `RecipientListOption`. */
interface RecipientListOption {
  readonly id: number;
  readonly displayName: string;
}

/** Mirrors `RecipientListStatuses`: current (now) statuses, counted like the native recipient list overview. */
export interface RecipientListStatuses {
  readonly receiving: number;
  readonly bounced: number;
  readonly unsubscribed: number;
  readonly notConfirmed: number;
}

/** Mirrors `RecipientListSummary`. */
export interface RecipientListSummary {
  readonly id: number;
  readonly displayName: string;
  readonly statuses: RecipientListStatuses;
  readonly subscriptions: number;
  readonly unsubscriptions: number;
  /** Subscribers on the range end. */
  readonly subscribers: number;
  /** Subscribers on the previous period end. */
  readonly previousSubscribers: number;
  /** Native recipient list, relative to the admin root. */
  readonly adminPath: string | null;
}

/** Mirrors `RecipientListsTotals`. */
interface RecipientListsTotals {
  readonly subscriptions: StatsValueComparison;
  readonly unsubscriptions: StatsValueComparison;
  /** Ratio; values are `null` for a period without subscriptions. Change in percentage points. */
  readonly unsubscribeRate: StatsValueComparison;
  /** Point in time: subscribers on the range end vs on the previous period end. */
  readonly subscribers: StatsValueComparison;
}

/** Mirrors `RecipientListsResult`. */
interface RecipientListsResult {
  readonly from: string;
  readonly to: string;
  readonly grouping: StatsGrouping;
  /** Applied list filter, `null` for all lists. */
  readonly recipientListId: number | null;
  readonly listOptions: readonly RecipientListOption[];
  readonly periods: readonly StatsPeriod[];
  readonly subscriptions: StatsValueSeries;
  readonly unsubscriptions: StatsValueSeries;
  /** Point in time: subscribers at each period end. `total` = value on the range end. */
  readonly subscribers: StatsValueSeries;
  readonly totals: RecipientListsTotals;
  /** All lists (filter not applied), most receiving first. */
  readonly byList: readonly RecipientListSummary[];
  /** Current statuses (list filter applied; all lists = contact + list pairs). */
  readonly statuses: RecipientListStatuses;
  /** `false` when the recipient list tables do not exist. */
  readonly available: boolean;
  /** Native Recipient lists listing, relative to the admin root. */
  readonly recipientListsAppPath: string | null;
  /** ISO timestamp of when the data was read from the database. */
  readonly updatedAt: string;
}

/** Mirrors `RecipientListsFilter`. */
interface RecipientListsFilter {
  readonly range: StatsFilter;
  readonly recipientListId: number | null;
}

/** Mirrors `RecipientListsClientProperties`. */
interface RecipientListsTemplateProps {
  readonly report: RecipientListsResult;
  readonly today: string;
  /** Path of this page relative to the admin root, used to build admin links. */
  readonly pagePath: string | null;
}

const recipientListsDocsUrl =
  'https://docs.kentico.com/documentation/business-users/digital-marketing/emails/send-regular-emails-to-subscribers';

const eventsHint =
  'A subscription is a confirmed subscription of a contact (after double opt-in, when the list uses it). An unsubscription is a revoked subscription. Subscribing again counts again. Only subscriptions stored by Xperience count: custom code that deletes subscription records instead of revoking them leaves no unsubscriptions.';

const subscribersHint =
  'Subscribers on a day are contacts who are members of the list now and whose latest subscription or unsubscription on or before that day is a subscription. Membership is current state (it has no history), and bounces are not applied. With all lists, each contact subscribed to at least one list is counted once.';

const statusHint =
  'Current state of list members, counted like the recipient list overview in the Recipient lists application. Receiving: subscribed and the email has not bounced. Bounced: subscribed, but the email had a hard bounce or reached the soft bounce limit. Unsubscribed: the latest action is an unsubscription. Not confirmed: a member without a confirmed subscription (double opt-in pending, or added without confirmation).';

const listCaptions: StatsRankedCaptions = { label: 'Recipient list', value: 'Receiving' };

function toFilter(report: RecipientListsResult): RecipientListsFilter {
  // Recipient lists have no channel; the range filter type is shared with channel reports.
  return {
    range: { from: report.from, to: report.to, grouping: report.grouping, channelId: null },
    recipientListId: report.recipientListId,
  };
}

export const RecipientListsTemplate = (props: RecipientListsTemplateProps) => {
  const saveCsv = useCsvExport();
  const { data: report, isLoading, hasError, load } = useStatsCommand<RecipientListsResult, RecipientListsFilter>(
    props.report,
  );
  const [filter, setFilter] = useState<RecipientListsFilter>(() => toFilter(props.report));

  const handleFilterChange = (next: RecipientListsFilter) => {
    setFilter(next);
    void load(next);
  };

  const handleRefresh = () => {
    void load(filter, { refresh: true });
  };

  const { totals, byList, periods, statuses } = report;
  const { pagePath } = props;
  const appHref = toAdminHref(report.recipientListsAppPath, pagePath);
  const rangeText = `${report.from} – ${report.to}`;
  const period = report.grouping.toLowerCase();
  const listName = report.listOptions.find((l) => l.id === report.recipientListId)?.displayName;
  const listSuffix = report.recipientListId === null ? '' : `_list-${report.recipientListId}`;
  const filteredHint = listName ? ` Only list ${listName}.` : '';
  const pairsHint = listName ? '' : ' With all lists, each contact is counted once per list.';

  const subscriptionSeries = useMemo(() => toValueSeries(report.subscriptions), [report.subscriptions]);
  const unsubscriptionSeries = useMemo(() => toValueSeries(report.unsubscriptions), [report.unsubscriptions]);
  const eventSeries = useMemo(() => [subscriptionSeries, unsubscriptionSeries], [subscriptionSeries, unsubscriptionSeries]);
  const subscriberSeries = useMemo(() => toValueSeries(report.subscribers), [report.subscribers]);
  const subscriberTableSeries = useMemo(() => [subscriberSeries], [subscriberSeries]);

  const statusSlices = useMemo<StatsShareSlice[]>(
    () => [
      { key: 'receiving', name: 'Receiving', value: statuses.receiving },
      { key: 'bounced', name: 'Bounced', value: statuses.bounced },
      { key: 'unsubscribed', name: 'Unsubscribed', value: statuses.unsubscribed },
      { key: 'not-confirmed', name: 'Not confirmed', value: statuses.notConfirmed },
    ],
    [statuses],
  );
  const memberCount = statusSlices.reduce((sum, slice) => sum + slice.value, 0);

  // The bar chart reuses the shared ranked chart: receiving members per list.
  const listItems = useMemo<StatsRankedItem[]>(() => {
    const receiving = byList.reduce((sum, list) => sum + list.statuses.receiving, 0);
    return byList.map((list, index) => ({
      rank: index + 1,
      key: `list:${list.id}`,
      label: list.displayName,
      secondaryLabel: null,
      value: list.statuses.receiving,
      secondaryValue: null,
      share: receiving > 0 ? list.statuses.receiving / receiving : 0,
      url: null,
      adminPath: list.adminPath,
    }));
  }, [byList]);

  const getListHref = useCallback((item: StatsRankedItem) => toAdminHref(item.adminPath, pagePath), [pagePath]);
  const getSummaryHref = useCallback((list: RecipientListSummary) => toAdminHref(list.adminPath, pagePath), [pagePath]);

  const noTables = 'Recipient list tables were not found in this project, so there are no recipient lists to show.';
  const noLists = report.available
    ? report.listOptions.length === 0
      ? 'There are no recipient lists yet. Create recipient lists in the Recipient lists application.'
      : null
    : noTables;
  const eventsEmpty =
    noLists ?? `No subscriptions or unsubscriptions in the selected range.${filteredHint} Try a longer range.`;
  const subscribersEmpty = noLists ?? `No contacts were subscribed on any day of the selected range.${filteredHint}`;
  const statusEmpty = noLists ?? `The list has no members.${filteredHint}`;

  const fileSuffix = `${listSuffix}_${report.from}_${report.to}`;

  const exportEventsCsv = () => {
    saveCsv(
      'recipient-lists-events',
      `recipient-lists-events${fileSuffix}_${period}.csv`,
      toTimeSeriesCsv(periods, eventSeries, { includeTotal: false }),
    );
  };

  const exportSubscribersCsv = () => {
    // Point-in-time values per period end; no total column (the values do not add up).
    saveCsv(
      'recipient-lists-subscribers',
      `recipient-lists-subscribers${fileSuffix}_${period}.csv`,
      toTimeSeriesCsv(periods, subscriberTableSeries, { includeTotal: false }),
    );
  };

  const exportListsCsv = () => {
    saveCsv(
      'recipient-lists',
      `recipient-lists_${report.from}_${report.to}.csv`,
      toCsv(
        [
          'Recipient list',
          'Receiving',
          'Bounced',
          'Unsubscribed',
          'Not confirmed',
          'Subscriptions',
          'Unsubscriptions',
          'Subscriber change',
          'Subscribers before range',
          `Subscribers ${report.to}`,
          'URL',
        ],
        byList.map((list) => [
            list.displayName,
            list.statuses.receiving,
            list.statuses.bounced,
            list.statuses.unsubscribed,
            list.statuses.notConfirmed,
            list.subscriptions,
            list.unsubscriptions,
            list.subscribers - list.previousSubscribers,
            list.previousSubscribers,
            list.subscribers,
            toAbsoluteUrl(getSummaryHref(list)),
          ]),
      ),
    );
  };

  const exportStatusCsv = () => {
    saveCsv('recipient-lists-status', `recipient-lists-status${listSuffix}_${props.today}.csv`, toShareCsv(statusSlices, 'Status', 'Members'));
  };

  return (
    <div className="AdminStats-root">
      <StatsFilterBar
        filter={filter.range}
        today={props.today}
        onChange={(range) => handleFilterChange({ ...filter, range })}
        onRefresh={handleRefresh}
        isLoading={isLoading}
        updatedAt={report.updatedAt}
        showChannel={false}
        actions={
          appHref && (
            <LinkButton
              label="Open recipient lists"
              color={ButtonColor.Tertiary}
              href={appHref}
              title="Open the Recipient lists application"
            />
          )
        }
      >
        {report.listOptions.length > 0 && (
          <IdSelect
            label="Recipient list"
            allLabel="All lists"
            options={report.listOptions.map((l) => ({ id: l.id, label: l.displayName }))}
            value={filter.recipientListId}
            onChange={(recipientListId) => handleFilterChange({ ...filter, recipientListId })}
          />
        )}
      </StatsFilterBar>

      <div className="AdminStats-kpis">
        <ComparisonInfoCard
          caption="Subscriptions"
          tooltip={`Confirmed subscriptions in the selected range (${rangeText}). Subscribing again counts again.${filteredHint}`}
          comparison={totals.subscriptions}
          noun="subscriptions"
        />
        <ComparisonInfoCard
          caption="Unsubscriptions"
          tooltip={`Unsubscriptions in the selected range (${rangeText}).${filteredHint}`}
          comparison={totals.unsubscriptions}
          noun="unsubscriptions"
        />
        <ComparisonInfoCard
          caption="Unsubscribe rate"
          tooltip={`Unsubscriptions divided by subscriptions in the selected range. The change is in percentage points (pp).${filteredHint}`}
          comparison={totals.unsubscribeRate}
          noun="subscriptions"
        />
        <ComparisonInfoCard
          caption="Subscribers"
          tooltip={`${subscribersHint} Counted on the last day of the range (${report.to}) and compared with the last day of the previous period.${filteredHint}`}
          comparison={totals.subscribers}
          noun="subscribers"
        />
      </div>

      <div className="AdminStats-tiles">
        <StatsTile
          headline="Subscriptions and unsubscriptions"
          description={`Subscriptions and unsubscriptions per ${period}, stacked. ${eventsHint}${filteredHint}`}
          isLoading={isLoading}
          hasError={hasError}
          isEmpty={report.subscriptions.total === 0 && report.unsubscriptions.total === 0}
          emptyMessage={eventsEmpty}
          onExportCsv={exportEventsCsv}
          renderChart={() => (
            <StackedColumnChart
              periods={periods}
              series={eventSeries}
              ariaLabel={`Subscriptions and unsubscriptions, ${rangeText}`}
            />
          )}
          renderTable={() => <TimeSeriesTable grouping={report.grouping} periods={periods} series={eventSeries} showTotal={false} />}
        />

        <StatsTile
          headline="Subscribers over time"
          description={`${subscribersHint} Values are counts at the end of each ${period} (the range end for the last one), so they do not add up.${filteredHint}`}
          isLoading={isLoading}
          hasError={hasError}
          isEmpty={report.subscribers.values.every((v) => v === 0)}
          emptyMessage={subscribersEmpty}
          onExportCsv={exportSubscribersCsv}
          renderChart={() => <ComboChart periods={periods} line={subscriberSeries} ariaLabel={`Subscribers, ${rangeText}`} />}
          renderTable={() => (
            <TimeSeriesTable grouping={report.grouping} periods={periods} series={subscriberTableSeries} showTotal={false} />
          )}
        />

        <StatsTile
          headline="Subscriber status"
          description={`${statusHint} Current state (now), not limited to the range.${pairsHint}${filteredHint}`}
          isLoading={isLoading}
          hasError={hasError}
          isEmpty={memberCount === 0}
          emptyMessage={statusEmpty}
          onExportCsv={exportStatusCsv}
          renderChart={() => (
            <DonutChart
              slices={statusSlices}
              centerValue={formatShare(statuses.receiving / memberCount)}
              centerCaption="receiving"
              ariaLabel="Subscriber status, now"
            />
          )}
          renderTable={() => <ShareTable slices={statusSlices} labelCaption="Status" valueCaption="Members" />}
        />
      </div>

      <StatsTile
        headline="Recipient lists"
        description={`All recipient lists, also when a list is selected in the filter. Receiving, bounced, unsubscribed and not confirmed are current members (now), like the recipient list overview; subscriptions and unsubscriptions are in the selected range; subscriber change is subscribers on the last day of the range minus subscribers on the day before the range. A contact can be in several lists.`}
        isLoading={isLoading}
        hasError={hasError}
        isEmpty={byList.length === 0}
        emptyMessage={noLists ?? 'There are no recipient lists yet.'}
        onExportCsv={exportListsCsv}
        defaultView="table"
        renderChart={() => (
          <RankedBarChart
            items={listItems}
            captions={listCaptions}
            getHref={getListHref}
            showShare={false}
            ariaLabel="Receiving members per recipient list, now"
          />
        )}
        renderTable={() => <RecipientListTable lists={byList} getAdminHref={getSummaryHref} />}
      />

      <DataRetentionNote
        message="The report reads stored subscriptions and unsubscriptions (subscription confirmations) of contacts. Deleting a contact also deletes its subscriptions, also for past dates, so numbers can be lower than they were; merged contacts keep their subscriptions. Custom code that deletes subscription records instead of revoking them hides those unsubscriptions. List membership and bounces are current state only, so they are applied as they are now."
        link={{ href: recipientListsDocsUrl, label: 'Learn about recipient lists and subscriptions' }}
      />
    </div>
  );
};
