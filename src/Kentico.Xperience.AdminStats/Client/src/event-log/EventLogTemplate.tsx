import {
  ButtonColor,
  Colors,
  LinkButton,
  NameToggleButton,
} from '@kentico/xperience-admin-components';
import React, { useCallback, useMemo, useState } from 'react';

import { toAdminHref } from '../shared/adminLinks';
import { ComparisonInfoCard } from '../shared/ComparisonInfoCard';
import { downloadCsv, toRankedCsv, toTimeSeriesCsv } from '../shared/csv';
import { DataRetentionNote } from '../shared/DataRetentionNote';
import { allOptionId, OptionToggle } from '../shared/filterControls';
import { formatPreviousPeriod, numberFormat } from '../shared/format';
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
import { TopSourcesTile } from './TopSourcesTile';
import '../shared/stats.css';

/** Mirrors `EventLogTypeTotal`. */
interface EventLogTypeTotal {
  /** `E`, `W` or `I`. */
  readonly eventType: string;
  readonly displayName: string;
  readonly comparison: StatsComparison;
}

/** Mirrors `EventLogResult`. */
interface EventLogResult {
  /** Applied event type filter (`E`, `W`, `I`), `null` for all. */
  readonly eventType: string | null;
  /** One series per type (information, warnings, errors: bottom to top); only the filtered type when filtering. */
  readonly trend: StatsTimeSeriesResult;
  /** Every type, not affected by the type filter. */
  readonly totals: readonly EventLogTypeTotal[];
  readonly totalComparison: StatsComparison;
  /** Items have `previousValue` and `change`. */
  readonly topSources: StatsRankedResult;
  /** Like `topSources`, only sources logged by Xperience (`CMS.*`, `Kentico.*`). */
  readonly topXperienceSources: StatsRankedResult;
  /** Like `topSources`, only other (custom) sources. */
  readonly topCustomSources: StatsRankedResult;
  /** Items have `previousValue` and `change`. */
  readonly topCodes: StatsRankedResult;
  /** Items have `previousValue` and `change`. Events without a user are one "System" row. */
  readonly topUsers: StatsRankedResult;
  /** "Event log size" setting: maximum events kept. 0 = not logged. `null` when unknown. */
  readonly logSizeLimit: number | null;
  /** Native Event log listing, relative to the admin root. */
  readonly eventLogPath: string | null;
  /** ISO timestamp of when the data was read from the database. */
  readonly updatedAt: string;
}

/** Mirrors `EventLogFilter`. */
interface EventLogFilter {
  readonly range: StatsFilter;
  readonly eventType: string | null;
}

/** Mirrors `EventLogClientProperties`. */
interface EventLogTemplateProps {
  readonly report: EventLogResult;
  readonly today: string;
  /** Path of this page relative to the admin root, used to build admin links. */
  readonly pagePath: string | null;
}

/** Fixed colors per type code (`CMS.EventLog.EventType`), so severities keep their color in every filter. */
const typeColors: Readonly<Record<string, Colors>> = {
  E: Colors.AlertBackgroundHighEmphasis,
  W: Colors.WarningBackgroundHighEmphasis,
  I: Colors.InfoBackgroundHighEmphasis,
};

/** Plural nouns per type code, for KPI details and empty messages. */
const typeNouns: Readonly<Record<string, string>> = {
  E: 'errors',
  W: 'warnings',
  I: 'information events',
};

const typeItems: NameToggleButton[] = [
  { id: allOptionId, label: 'All' },
  { id: 'E', label: 'Errors' },
  { id: 'W', label: 'Warnings' },
  { id: 'I', label: 'Information' },
];

const eventLogDocsUrl =
  'https://docs.kentico.com/documentation/developers-and-admins/configuration/event-log';

function toFilter(report: EventLogResult): EventLogFilter {
  // Events have no channel; the range filter type is shared with channel reports.
  return {
    range: {
      from: report.trend.from,
      to: report.trend.to,
      grouping: report.trend.grouping,
      channelId: null,
    },
    eventType: report.eventType,
  };
}

function capitalize(text: string): string {
  return text.charAt(0).toUpperCase() + text.slice(1);
}

function retentionMessage(limit: number | null): string {
  if (limit === 0) {
    return 'The Event log size setting is 0, so no events are logged and this report stays empty.';
  }
  const size =
    limit === null ? 'a maximum number of events' : `at most ${numberFormat.format(limit)} events`;
  return `The event log keeps ${size} (Event log size setting). When the log is full, the oldest events are deleted, so long ranges can show fewer events in older periods than really happened.`;
}

export const EventLogTemplate = (props: EventLogTemplateProps) => {
  const { data: report, isLoading, hasError, load } = useStatsCommand<
    EventLogResult,
    EventLogFilter
  >(props.report);
  const [filter, setFilter] = useState<EventLogFilter>(() => toFilter(props.report));


  const handleFilterChange = (next: EventLogFilter) => {
    setFilter(next);
    void load(next);
  };

  const handleRefresh = () => {
    void load(filter, { refresh: true });
  };

  const { pagePath } = props;
  const getAdminHref = useCallback(
    (item: StatsRankedItem) => toAdminHref(item.adminPath, pagePath),
    [pagePath],
  );
  const eventLogHref = toAdminHref(report.eventLogPath, pagePath);

  const { trend, topCodes, topUsers, totalComparison } = report;
  const rangeText = `${trend.from} – ${trend.to}`;
  const noun = report.eventType ? (typeNouns[report.eventType] ?? 'events') : 'events';
  const emptyMessage = `No ${noun} in the selected range. Try a longer range or another event type.`;
  const typeSuffix = report.eventType ? `_${report.eventType.toLowerCase()}` : '';

  const series = useMemo<StatsSeries[]>(
    () => toStatsSeries(trend.series).map((s) => ({ ...s, color: typeColors[s.key] })),
    [trend.series],
  );

  const changeCaptions = useMemo(
    () => ({
      value: 'Events',
      previousValue: capitalize(formatPreviousPeriod(totalComparison)),
      change: 'Change',
    }),
    [totalComparison],
  );
  const sourceCaptions = useMemo<StatsRankedCaptions>(
    () => ({ label: 'Source', ...changeCaptions }),
    [changeCaptions],
  );
  const codeCaptions = useMemo<StatsRankedCaptions>(
    () => ({ label: 'Event code', ...changeCaptions }),
    [changeCaptions],
  );
  const userCaptions = useMemo<StatsRankedCaptions>(
    () => ({ label: 'User', ...changeCaptions }),
    [changeCaptions],
  );

  const totalFor = (type: string) => report.totals.find((t) => t.eventType === type);

  const exportTrendCsv = () => {
    downloadCsv(
      `event-log${typeSuffix}_${trend.from}_${trend.to}_${trend.grouping.toLowerCase()}.csv`,
      toTimeSeriesCsv(trend.periods, series),
    );
  };

  const exportRankedCsv = (
    name: string,
    result: StatsRankedResult,
    captions: StatsRankedCaptions,
    getHref?: (item: StatsRankedItem) => string | null,
  ) => {
    downloadCsv(
      `event-log-${name}${typeSuffix}_${result.from}_${result.to}.csv`,
      toRankedCsv(result.items, captions, getHref),
    );
  };

  const filteredHint = report.eventType
    ? ` Only ${noun}, as selected in the Event type filter.`
    : '';
  const changeHint = `Change compares with the ${formatPreviousPeriod(totalComparison)} (${totalComparison.previousFrom} – ${totalComparison.previousTo}). "New" means none in that period.`;

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
          eventLogHref && (
            <LinkButton
              label="Open event log"
              color={ButtonColor.Tertiary}
              href={eventLogHref}
              title="Open the Event log application"
            />
          )
        }
      >
        <OptionToggle
          label="Event type"
          items={typeItems}
          value={filter.eventType}
          onChange={(eventType) => handleFilterChange({ ...filter, eventType })}
        />
      </StatsFilterBar>

      <div className="AdminStats-kpis">
        <ComparisonInfoCard
          caption="Events"
          tooltip={`All events (every type) in the selected range (${rangeText}).`}
          comparison={totalComparison}
          noun="events"
        />
        {['E', 'W', 'I'].map((type) => {
          const total = totalFor(type);
          return total ? (
            <ComparisonInfoCard
              key={type}
              caption={total.displayName}
              tooltip={`${total.displayName} events in the selected range (${rangeText}). Not affected by the Event type filter.`}
              comparison={total.comparison}
              noun={typeNouns[type] ?? 'events'}
            />
          ) : null;
        })}
      </div>

      <StatsTile
        headline="Events over time"
        description={`Events per ${trend.grouping.toLowerCase()}, stacked by type.${filteredHint}`}
        isLoading={isLoading}
        hasError={hasError}
        isEmpty={trend.total === 0}
        emptyMessage={emptyMessage}
        onExportCsv={exportTrendCsv}
        renderChart={() => (
          <StackedColumnChart
            periods={trend.periods}
            series={series}
            ariaLabel={`Events over time, ${rangeText}`}
          />
        )}
        renderTable={() => (
          <TimeSeriesTable grouping={trend.grouping} periods={trend.periods} series={series} />
        )}
      />

      <TopSourcesTile
        all={report.topSources}
        xperience={report.topXperienceSources}
        custom={report.topCustomSources}
        captions={sourceCaptions}
        isLoading={isLoading}
        hasError={hasError}
        noun={noun}
        emptyMessage={emptyMessage}
        descriptionSuffix={`${changeHint}${filteredHint}`}
        rangeText={rangeText}
        onExportCsv={(name, result) => exportRankedCsv(name, result, sourceCaptions)}
      />

      <StatsTile
        headline="Top event codes"
        description={`Event codes with the most events. ${changeHint}${filteredHint}`}
        isLoading={isLoading}
        hasError={hasError}
        isEmpty={topCodes.items.length === 0}
        emptyMessage={emptyMessage}
        onExportCsv={() => exportRankedCsv('codes', topCodes, codeCaptions)}
        renderChart={() => (
          <RankedBarChart
            items={topCodes.items}
            captions={codeCaptions}
            ariaLabel={`Top event codes, ${rangeText}`}
          />
        )}
        renderTable={() => <RankedTable items={topCodes.items} captions={codeCaptions} />}
      />

      <StatsTile
        headline="Top users"
        description={`Users with the most events. ${changeHint} Events logged without a user (for example by background tasks) are shown as System. Click a user's bar or name in the table to open the user.${filteredHint}`}
        isLoading={isLoading}
        hasError={hasError}
        isEmpty={topUsers.items.length === 0}
        emptyMessage={emptyMessage}
        onExportCsv={() => exportRankedCsv('users', topUsers, userCaptions, getAdminHref)}
        renderChart={() => (
          <RankedBarChart
            items={topUsers.items}
            captions={userCaptions}
            ariaLabel={`Top users by events, ${rangeText}`}
            getHref={getAdminHref}
          />
        )}
        renderTable={() => (
          <RankedTable items={topUsers.items} captions={userCaptions} getAdminHref={getAdminHref} />
        )}
      />

      <DataRetentionNote
        message={retentionMessage(report.logSizeLimit)}
        link={{ href: eventLogDocsUrl, label: 'Learn about the event log settings' }}
      />
    </div>
  );
};
