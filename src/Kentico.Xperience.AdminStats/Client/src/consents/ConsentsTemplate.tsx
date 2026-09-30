import { ButtonColor, LinkButton } from '@kentico/xperience-admin-components';
import React, { useCallback, useMemo, useState } from 'react';

import { toAdminHref } from '../shared/adminLinks';
import { ComboChart } from '../shared/ComboChart';
import { ComparisonInfoCard } from '../shared/ComparisonInfoCard';
import { CoverageBarChart, CoverageCaptions } from '../shared/CoverageBarChart';
import { CoverageTable } from '../shared/CoverageTable';
import { downloadCsv, toCoverageCsv, toRankedCsv, toTimeSeriesCsv } from '../shared/csv';
import { DataRetentionNote } from '../shared/DataRetentionNote';
import { IdSelect } from '../shared/filterControls';
import { formatPreviousPeriod } from '../shared/format';
import { RankedBarChart } from '../shared/RankedBarChart';
import { RankedTable } from '../shared/RankedTable';
import { StackedColumnChart } from '../shared/StackedColumnChart';
import { StatsFilterBar } from '../shared/StatsFilterBar';
import { StatsTile } from '../shared/StatsTile';
import { toValueSeries } from '../shared/timeSeries';
import { TimeSeriesTable } from '../shared/TimeSeriesTable';
import {
  StatsCoverageItem,
  StatsFilter,
  StatsGrouping,
  StatsPeriod,
  StatsRankedCaptions,
  StatsRankedItem,
  StatsRankedResult,
  StatsValueComparison,
  StatsValueSeries,
} from '../shared/types';
import { useStatsCommand } from '../shared/useStatsCommand';
import '../shared/stats.css';

/** Mirrors `ConsentOption`. */
interface ConsentOption {
  readonly id: number;
  readonly displayName: string;
}

/** Mirrors `ConsentsTotals`. */
interface ConsentsTotals {
  readonly agreements: StatsValueComparison;
  readonly revocations: StatsValueComparison;
  /** Ratio; values are `null` for a period without agreements. Change in percentage points. */
  readonly revocationRate: StatsValueComparison;
  /** Point in time: agreed contacts on the range end vs on the previous period end. */
  readonly agreedContacts: StatsValueComparison;
}

/** Mirrors `ConsentsResult`. */
interface ConsentsResult {
  readonly from: string;
  readonly to: string;
  readonly grouping: StatsGrouping;
  /** Applied consent filter, `null` for all consents. */
  readonly consentId: number | null;
  readonly consents: readonly ConsentOption[];
  readonly periods: readonly StatsPeriod[];
  readonly agreements: StatsValueSeries;
  readonly revocations: StatsValueSeries;
  /** Point in time: agreed contacts at each period end. `total` = value on the range end. */
  readonly agreedContacts: StatsValueSeries;
  readonly totals: ConsentsTotals;
  /** All consents (filter not applied): agreed contacts with previous and change, agreements, revocations. */
  readonly byConsent: StatsRankedResult;
  /** Per consent: agreed contacts on the current text of all agreed contacts. */
  readonly textVersions: readonly StatsCoverageItem[];
  /** `false` when the consent tables do not exist. */
  readonly available: boolean;
  /** Native Data protection consent listing, relative to the admin root. */
  readonly dataProtectionPath: string | null;
  /** ISO timestamp of when the data was read from the database. */
  readonly updatedAt: string;
}

/** Mirrors `ConsentsFilter`. */
interface ConsentsFilter {
  readonly range: StatsFilter;
  readonly consentId: number | null;
}

/** Mirrors `ConsentsClientProperties`. */
interface ConsentsTemplateProps {
  readonly report: ConsentsResult;
  readonly today: string;
  /** Path of this page relative to the admin root, used to build admin links. */
  readonly pagePath: string | null;
}

const consentDocsUrl = 'https://docs.kentico.com/documentation/developers-and-admins/data-protection/consent-management';

const eventsHint =
  'An agreement or revocation is one agree or revoke action of a contact. Agreeing again (for example to a new consent text) counts again.';

const agreedHint =
  "Agreed contacts on a day are contacts whose latest agreement or revocation of the consent on or before that day is an agreement. With all consents, each contact who agrees to at least one consent is counted once.";

const textCaptions: CoverageCaptions = {
  covered: 'Current text',
  missing: 'Older text',
  totalNoun: 'agreed contacts',
};

function toFilter(report: ConsentsResult): ConsentsFilter {
  // Consents have no channel; the range filter type is shared with channel reports.
  return {
    range: { from: report.from, to: report.to, grouping: report.grouping, channelId: null },
    consentId: report.consentId,
  };
}

export const ConsentsTemplate = (props: ConsentsTemplateProps) => {
  const { data: report, isLoading, hasError, load } = useStatsCommand<ConsentsResult, ConsentsFilter>(props.report);
  const [filter, setFilter] = useState<ConsentsFilter>(() => toFilter(props.report));

  const handleFilterChange = (next: ConsentsFilter) => {
    setFilter(next);
    void load(next);
  };

  const handleRefresh = () => {
    void load(filter, { refresh: true });
  };

  const { totals, byConsent, periods } = report;
  const { pagePath } = props;
  const dataProtectionHref = toAdminHref(report.dataProtectionPath, pagePath);
  const rangeText = `${report.from} – ${report.to}`;
  const period = report.grouping.toLowerCase();
  const previousPeriod = formatPreviousPeriod(totals.agreements);
  const consentName = report.consents.find((c) => c.id === report.consentId)?.displayName;
  const consentSuffix = report.consentId === null ? '' : `_consent-${report.consentId}`;
  const filteredHint = consentName ? ` Only consent ${consentName}.` : '';

  const agreementSeries = useMemo(() => toValueSeries(report.agreements), [report.agreements]);
  const revocationSeries = useMemo(() => toValueSeries(report.revocations), [report.revocations]);
  const eventSeries = useMemo(() => [agreementSeries, revocationSeries], [agreementSeries, revocationSeries]);
  const agreedSeries = useMemo(() => toValueSeries(report.agreedContacts), [report.agreedContacts]);
  const agreedTableSeries = useMemo(() => [agreedSeries], [agreedSeries]);

  const consentCaptions = useMemo<StatsRankedCaptions>(
    () => ({
      label: 'Consent',
      value: 'Agreed contacts',
      secondaryValue: 'Agreements',
      tertiaryValue: 'Revocations',
      previousValue: `Agreed contacts ${previousPeriod} end`,
      change: 'Change',
    }),
    [previousPeriod],
  );

  const getConsentHref = useCallback((item: StatsRankedItem) => toAdminHref(item.adminPath, pagePath), [pagePath]);

  const noTables = 'Consent tables were not found in this project, so there are no consents to show.';
  const noConsents = report.available
    ? report.consents.length === 0
      ? 'There are no consents yet. Create consents in the Data protection application.'
      : null
    : noTables;
  const eventsEmpty =
    noConsents ?? `No agreements or revocations in the selected range.${filteredHint} Try a longer range.`;
  const agreedEmpty = noConsents ?? `No contacts agreed on any day of the selected range.${filteredHint}`;

  const fileSuffix = `${consentSuffix}_${report.from}_${report.to}`;

  const exportEventsCsv = () => {
    downloadCsv(`consents-events${fileSuffix}_${period}.csv`, toTimeSeriesCsv(periods, eventSeries, { includeTotal: false }));
  };

  const exportAgreedCsv = () => {
    // Point-in-time values per period end; no total column (the values do not add up).
    downloadCsv(
      `consents-agreed-contacts${fileSuffix}_${period}.csv`,
      toTimeSeriesCsv(periods, agreedTableSeries, { includeTotal: false }),
    );
  };

  const exportConsentsCsv = () => {
    downloadCsv(`consents_${report.from}_${report.to}.csv`, toRankedCsv(byConsent.items, consentCaptions, getConsentHref));
  };

  const exportTextsCsv = () => {
    downloadCsv(
      `consents-text-versions${consentSuffix}_${report.to}.csv`,
      toCoverageCsv(report.textVersions, { label: 'Consent', covered: textCaptions.covered, missing: textCaptions.missing }),
    );
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
          dataProtectionHref && (
            <LinkButton
              label="Open data protection"
              color={ButtonColor.Tertiary}
              href={dataProtectionHref}
              title="Open the consents of the Data protection application"
            />
          )
        }
      >
        {report.consents.length > 0 && (
          <IdSelect
            label="Consent"
            allLabel="All consents"
            options={report.consents.map((c) => ({ id: c.id, label: c.displayName }))}
            value={filter.consentId}
            onChange={(consentId) => handleFilterChange({ ...filter, consentId })}
          />
        )}
      </StatsFilterBar>

      <div className="AdminStats-kpis">
        <ComparisonInfoCard
          caption="Agreements"
          tooltip={`Agree actions in the selected range (${rangeText}). Agreeing again counts again.${filteredHint}`}
          comparison={totals.agreements}
          noun="agreements"
        />
        <ComparisonInfoCard
          caption="Revocations"
          tooltip={`Revoke actions in the selected range (${rangeText}).${filteredHint}`}
          comparison={totals.revocations}
          noun="revocations"
        />
        <ComparisonInfoCard
          caption="Revocation rate"
          tooltip={`Revocations divided by agreements in the selected range. The change is in percentage points (pp).${filteredHint}`}
          comparison={totals.revocationRate}
          noun="agreements"
        />
        <ComparisonInfoCard
          caption="Agreed contacts"
          tooltip={`${agreedHint} Counted on the last day of the range (${report.to}) and compared with the last day of the previous period.${filteredHint}`}
          comparison={totals.agreedContacts}
          noun="agreed contacts"
        />
      </div>

      <div className="AdminStats-tiles">
        <StatsTile
          headline="Agreements and revocations"
          description={`Agreements and revocations per ${period}, stacked. ${eventsHint}${filteredHint}`}
          isLoading={isLoading}
          hasError={hasError}
          isEmpty={report.agreements.total === 0 && report.revocations.total === 0}
          emptyMessage={eventsEmpty}
          onExportCsv={exportEventsCsv}
          renderChart={() => (
            <StackedColumnChart
              periods={periods}
              series={eventSeries}
              ariaLabel={`Agreements and revocations, ${rangeText}`}
            />
          )}
          renderTable={() => <TimeSeriesTable grouping={report.grouping} periods={periods} series={eventSeries} showTotal={false} />}
        />

        <StatsTile
          headline="Agreed contacts over time"
          description={`${agreedHint} Values are counts at the end of each ${period} (the range end for the last one), so they do not add up.${filteredHint}`}
          isLoading={isLoading}
          hasError={hasError}
          isEmpty={report.agreedContacts.values.every((v) => v === 0)}
          emptyMessage={agreedEmpty}
          onExportCsv={exportAgreedCsv}
          renderChart={() => (
            <ComboChart periods={periods} line={agreedSeries} ariaLabel={`Agreed contacts, ${rangeText}`} />
          )}
          renderTable={() => (
            <TimeSeriesTable grouping={report.grouping} periods={periods} series={agreedTableSeries} showTotal={false} />
          )}
        />

        <StatsTile
          headline="Consent text version"
          description={`Agreed contacts on the last day of the range whose latest agreement was given to the current consent text or to an older text. Contacts on an older text may need to agree again.${filteredHint}`}
          isLoading={isLoading}
          hasError={hasError}
          isEmpty={report.textVersions.length === 0}
          emptyMessage={agreedEmpty}
          onExportCsv={exportTextsCsv}
          renderChart={() => (
            <CoverageBarChart
              items={report.textVersions}
              captions={textCaptions}
              ariaLabel={`Consent text version of agreed contacts, ${report.to}`}
            />
          )}
          renderTable={() => <CoverageTable items={report.textVersions} labelCaption="Consent" captions={textCaptions} />}
        />
      </div>

      <StatsTile
        headline="Consents"
        description={`All consents, also when a consent is selected in the filter. Agreed contacts on the last day of the range, compared with the last day of the ${previousPeriod}; agreements and revocations in the range. A contact can agree to several consents, so shares are of all agreed contacts and do not add up to 100%.`}
        isLoading={isLoading}
        hasError={hasError}
        isEmpty={byConsent.items.length === 0}
        emptyMessage={noConsents ?? 'There are no consents yet.'}
        onExportCsv={exportConsentsCsv}
        defaultView="table"
        renderChart={() => (
          <RankedBarChart
            items={byConsent.items}
            captions={consentCaptions}
            getHref={getConsentHref}
            ariaLabel={`Agreed contacts per consent, ${report.to}`}
          />
        )}
        renderTable={() => <RankedTable items={byConsent.items} captions={consentCaptions} getAdminHref={getConsentHref} />}
      />

      <DataRetentionNote
        message="The report reads the stored agreements and revocations of contacts. Deleting a contact also deletes its consent agreements, also for past dates, so numbers can be lower than they were; merged contacts keep their agreements. The report compares text versions by hash only and does not show what changed in the text. Revoking can trigger data erasure in projects that handle it."
        link={{ href: consentDocsUrl, label: 'Learn how to manage consents' }}
      />
    </div>
  );
};
