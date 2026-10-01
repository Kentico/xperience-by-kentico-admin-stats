import { ButtonColor, InfoCard, LinkButton } from '@kentico/xperience-admin-components';
import React, { useCallback, useMemo, useState } from 'react';

import { toAdminHref } from '../shared/adminLinks';
import { ComboChart } from '../shared/ComboChart';
import { ComparisonInfoCard } from '../shared/ComparisonInfoCard';
import { toRankedCsv, toShareCsv, toTimeSeriesCsv } from '../shared/csv';
import { DataRetentionNote } from '../shared/DataRetentionNote';
import { DonutChart } from '../shared/DonutChart';
import { formatShare, numberFormat } from '../shared/format';
import { RankedBarChart } from '../shared/RankedBarChart';
import { RankedTable } from '../shared/RankedTable';
import { ShareTable } from '../shared/ShareTable';
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
  StatsRankedResult,
  StatsShareSlice,
  StatsValueComparison,
  StatsValueSeries,
} from '../shared/types';
import { useCsvExport } from '../shared/useCsvExport';
import { useStatsCommand } from '../shared/useStatsCommand';
import '../shared/stats.css';

/** Mirrors `MembersTotals`. */
interface MembersTotals {
  readonly newMembers: StatsValueComparison;
  /** Point in time: members on the range end vs on the previous period end. */
  readonly totalMembers: StatsValueComparison;
  /** Ratio; values are `null` for a period without new members. Change in percentage points. */
  readonly externalShare: StatsValueComparison;
  /** Current state, no comparison. */
  readonly disabledMembers: number;
}

/** Mirrors `MembersResult`. */
interface MembersResult {
  readonly from: string;
  readonly to: string;
  readonly grouping: StatsGrouping;
  readonly periods: readonly StatsPeriod[];
  readonly newMembers: StatsValueSeries;
  readonly internalMembers: StatsValueSeries;
  readonly externalMembers: StatsValueSeries;
  /** Running total: members at the end of each period. */
  readonly totalMembers: StatsValueSeries;
  readonly totals: MembersTotals;
  /** Current members per role plus "No role"; secondary value = created in the range. Shares are of all members. */
  readonly byRole: StatsRankedResult;
  /** `false` when the member tables do not exist. */
  readonly available: boolean;
  /** Native Members listing, relative to the admin root. */
  readonly membersPath: string | null;
  /** ISO timestamp of when the data was read from the database. */
  readonly updatedAt: string;
}

/** Mirrors `MembersClientProperties`. */
interface MembersTemplateProps {
  readonly report: MembersResult;
  readonly today: string;
  /** Path of this page relative to the admin root, used to build admin links. */
  readonly pagePath: string | null;
}

const membersDocsUrl = 'https://docs.kentico.com/documentation/business-users/members';

const externalHint =
  'External members signed up through an external sign-in provider (for example Google or Microsoft). Internal members registered on the site.';

const roleCaptions: StatsRankedCaptions = {
  label: 'Role',
  value: 'Members',
  secondaryValue: 'New in range',
};

function toFilter(report: MembersResult): StatsFilter {
  // Members have no channel; the filter type is shared with channel reports.
  return { from: report.from, to: report.to, grouping: report.grouping, channelId: null };
}

export const MembersTemplate = (props: MembersTemplateProps) => {
  const saveCsv = useCsvExport();
  const { data: report, isLoading, hasError, load } = useStatsCommand<MembersResult>(props.report);
  const [filter, setFilter] = useState<StatsFilter>(() => toFilter(props.report));

  const handleFilterChange = (next: StatsFilter) => {
    setFilter(next);
    void load(next);
  };

  const handleRefresh = () => {
    void load(filter, { refresh: true });
  };

  const { totals, byRole, periods } = report;
  const { pagePath } = props;
  const membersHref = toAdminHref(report.membersPath, pagePath);
  const rangeText = `${report.from} – ${report.to}`;
  const period = report.grouping.toLowerCase();

  const newSeries = useMemo(() => toValueSeries(report.newMembers), [report.newMembers]);
  const internalSeries = useMemo(() => toValueSeries(report.internalMembers), [report.internalMembers]);
  const externalSeries = useMemo(() => toValueSeries(report.externalMembers), [report.externalMembers]);
  const totalSeries = useMemo(() => toValueSeries(report.totalMembers), [report.totalMembers]);
  const growthTableSeries = useMemo(
    () => [newSeries, internalSeries, externalSeries, totalSeries],
    [newSeries, internalSeries, externalSeries, totalSeries],
  );

  const slices = useMemo<StatsShareSlice[]>(
    () => [
      { key: report.internalMembers.key, name: report.internalMembers.displayName, value: report.internalMembers.total },
      { key: report.externalMembers.key, name: report.externalMembers.displayName, value: report.externalMembers.total },
    ],
    [report.internalMembers, report.externalMembers],
  );

  const getRoleHref = useCallback((item: StatsRankedItem) => toAdminHref(item.adminPath, pagePath), [pagePath]);

  const noMembers = 'Member tables were not found in this project, so there are no members to show.';
  const newEmpty = report.available
    ? 'No members registered in the selected range. Try a longer range.'
    : noMembers;

  const exportGrowthCsv = () => {
    saveCsv(
      'members-growth',
      `members-growth_${report.from}_${report.to}_${period}.csv`,
      toTimeSeriesCsv(periods, growthTableSeries, { includeTotal: false }),
    );
  };

  const exportSignInCsv = () => {
    saveCsv('members-sign-in-type', `members-sign-in-type_${report.from}_${report.to}.csv`, toShareCsv(slices, 'Sign-in type', 'New members'));
  };

  const exportRolesCsv = () => {
    saveCsv('members-by-role', `members-by-role_${report.to}.csv`, toRankedCsv(byRole.items, roleCaptions, getRoleHref));
  };

  const externalShare = totals.externalShare.current;

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
        actions={
          membersHref && (
            <LinkButton
              label="Open members"
              color={ButtonColor.Tertiary}
              href={membersHref}
              title="Open the Members application"
            />
          )
        }
      />

      <div className="AdminStats-kpis">
        <ComparisonInfoCard
          caption="New members"
          tooltip={`Members created in the selected range (${rangeText}) that still exist.`}
          comparison={totals.newMembers}
          noun="new members"
        />
        <ComparisonInfoCard
          caption="Total members"
          tooltip={`Members created on or before the last day of the range (${report.to}) that still exist, compared with the last day of the previous period. Deleted members are not counted.`}
          comparison={totals.totalMembers}
          noun="members"
        />
        <ComparisonInfoCard
          caption="External sign-ups"
          tooltip={`Share of new members who signed up through an external sign-in provider. The change is in percentage points (pp).`}
          comparison={totals.externalShare}
          noun="new members"
        />
        <InfoCard
          caption="Disabled members"
          tooltip="Members whose account is disabled now, so they cannot sign in. Current state, not limited to the range and not compared."
          text={numberFormat.format(totals.disabledMembers)}
          details="Current state"
        />
      </div>

      <div className="AdminStats-tiles">
        <StatsTile
          headline="Member growth"
          description={`New members per ${period} (columns, left axis) and total members at the end of each ${period} (line, right axis). The table splits new members into internal and external.`}
          isLoading={isLoading}
          hasError={hasError}
          isEmpty={!report.available || report.totalMembers.total === 0}
          emptyMessage={report.available ? 'There are no members yet.' : noMembers}
          onExportCsv={exportGrowthCsv}
          renderChart={() => (
            <ComboChart
              periods={periods}
              columns={newSeries}
              line={totalSeries}
              ariaLabel={`Member growth, ${rangeText}`}
            />
          )}
          renderTable={() => (
            <TimeSeriesTable grouping={report.grouping} periods={periods} series={growthTableSeries} showTotal={false} />
          )}
        />

        <StatsTile
          headline="New members by sign-in type"
          description={externalHint}
          isLoading={isLoading}
          hasError={hasError}
          isEmpty={report.newMembers.total === 0}
          emptyMessage={newEmpty}
          onExportCsv={exportSignInCsv}
          renderChart={() => (
            <DonutChart
              slices={slices}
              centerValue={externalShare === null ? '–' : formatShare(externalShare)}
              centerCaption="external"
              ariaLabel={`New members by sign-in type, ${rangeText}`}
            />
          )}
          renderTable={() => <ShareTable slices={slices} labelCaption="Sign-in type" valueCaption="New members" />}
        />

        <StatsTile
          headline="Members by role"
          description="Current members per member role (current state, not limited to the range). New in range = of them, members created in the selected range. A member can have several roles, so the share is of all members and shares do not add up to 100%."
          isLoading={isLoading}
          hasError={hasError}
          isEmpty={byRole.items.length === 0}
          emptyMessage={report.available ? 'There are no members yet.' : noMembers}
          onExportCsv={exportRolesCsv}
          renderChart={() => (
            <RankedBarChart items={byRole.items} captions={roleCaptions} getHref={getRoleHref} ariaLabel="Members by role" />
          )}
          renderTable={() => <RankedTable items={byRole.items} captions={roleCaptions} getAdminHref={getRoleHref} />}
        />
      </div>

      <DataRetentionNote
        message="Members are counted by the date their account was created. Deleted members are not counted, also not for past dates, so totals can be lower than they were. Disabled members and roles are the current state."
        link={{ href: membersDocsUrl, label: 'Learn how to manage members' }}
      />
    </div>
  );
};
