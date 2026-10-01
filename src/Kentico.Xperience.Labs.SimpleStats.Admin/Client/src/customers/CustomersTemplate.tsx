import { ButtonColor, LinkButton, NameToggleButton, NameToggleButtons } from '@kentico/xperience-admin-components';
import React, { useCallback, useMemo, useState } from 'react';

import { toAdminHref } from '../shared/adminLinks';
import { ComboChart } from '../shared/ComboChart';
import {
  CommerceOrderStatusOption,
  commerceDocsUrl,
  OrderStatusSelect,
  rawAmountNote,
} from '../shared/commerce';
import { ComparisonInfoCard } from '../shared/ComparisonInfoCard';
import { toRankedCsv, toTimeSeriesCsv } from '../shared/csv';
import { DataRetentionNote } from '../shared/DataRetentionNote';
import { OptionToggle } from '../shared/filterControls';
import { formatPreviousPeriod } from '../shared/format';
import { RankedBarChart } from '../shared/RankedBarChart';
import { RankedTable } from '../shared/RankedTable';
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
  StatsValueComparison,
  StatsValueSeries,
} from '../shared/types';
import { useCsvExport } from '../shared/useCsvExport';
import { useStatsCommand } from '../shared/useStatsCommand';
import { TopCustomersRankBy, TopCustomersTile } from './TopCustomersTile';
import '../shared/stats.css';

/** Mirrors `CustomersAddressType`. */
type CustomersAddressType = 'Billing' | 'Shipping';

/** Mirrors `CustomersTotals`. */
interface CustomersTotals {
  readonly newCustomers: StatsValueComparison;
  readonly orderingCustomers: StatsValueComparison;
  /** Ratio; values are `null` for a period without ordering customers. Change in percentage points. */
  readonly returningShare: StatsValueComparison;
  /** Amount; values are `null` for a period without ordering customers. */
  readonly revenuePerCustomer: StatsValueComparison;
  /** Point in time: active customers on the range end vs on the previous period end. */
  readonly activeCustomers: StatsValueComparison;
}

/** Mirrors `CustomersTopLists`. */
interface CustomersTopLists {
  readonly byRevenue: StatsRankedResult;
  readonly byOrders: StatsRankedResult;
  readonly byQuantity: StatsRankedResult;
}

/** Mirrors `CustomersResult`. */
interface CustomersResult {
  readonly from: string;
  readonly to: string;
  readonly grouping: StatsGrouping;
  /** Applied status filter, `null` for all statuses. */
  readonly orderStatusId: number | null;
  readonly addressType: CustomersAddressType;
  /** Applied activity window (days) of `activeCustomers`. */
  readonly activityWindowDays: number;
  readonly statuses: readonly CommerceOrderStatusOption[];
  readonly periods: readonly StatsPeriod[];
  readonly newCustomers: StatsValueSeries;
  /** Running total: customers at the end of each period. */
  readonly totalCustomers: StatsValueSeries;
  /** Point in time: customers with an order in the activity window up to each period end. `total` = value on the range end. */
  readonly activeCustomers: StatsValueSeries;
  readonly totals: CustomersTotals;
  /** Ordering customers per country ("Unknown" row for no address or no country), with previous count and change. */
  readonly byCountry: StatsRankedResult;
  /** Ordering customers per state ("State, Country"), customers without a state left out. */
  readonly topStates: StatsRankedResult;
  readonly topCustomers: CustomersTopLists;
  /** `false` when the commerce tables do not exist. */
  readonly commerceAvailable: boolean;
  /** Native Customers listing, relative to the admin root. */
  readonly customersPath: string | null;
  /** ISO timestamp of when the data was read from the database. */
  readonly updatedAt: string;
}

/** Mirrors `CustomersFilter`. */
interface CustomersFilter {
  readonly range: StatsFilter;
  readonly orderStatusId: number | null;
  readonly addressType: CustomersAddressType;
  readonly activityWindowDays: number;
}

/** Mirrors `CustomersClientProperties`. */
interface CustomersTemplateProps {
  readonly report: CustomersResult;
  readonly today: string;
  /** Path of this page relative to the admin root, used to build admin links. */
  readonly pagePath: string | null;
}

/** Mirrors `CustomersActivityWindow.Allowed`. */
const activityWindowItems: NameToggleButton[] = [30, 60, 90, 180].map((days) => ({
  id: String(days),
  label: `${days} days`,
}));

const addressTypeItems: NameToggleButton[] = [
  { id: 'Billing', label: 'Billing' },
  { id: 'Shipping', label: 'Shipping' },
];

function toFilter(report: CustomersResult): CustomersFilter {
  // Customers have no channel; the range filter type is shared with channel reports.
  return {
    range: { from: report.from, to: report.to, grouping: report.grouping, channelId: null },
    orderStatusId: report.orderStatusId,
    addressType: report.addressType,
    activityWindowDays: report.activityWindowDays,
  };
}

/** Captions of the location lists: customers with the previous period count and change. */
function toLocationCaptions(label: string, previousPeriod: string): StatsRankedCaptions {
  return { label, value: 'Customers', previousValue: `Customers ${previousPeriod}`, change: 'Change' };
}

export const CustomersTemplate = (props: CustomersTemplateProps) => {
  const saveCsv = useCsvExport();
  const { data: report, isLoading, hasError, load } = useStatsCommand<CustomersResult, CustomersFilter>(
    props.report,
  );
  const [filter, setFilter] = useState<CustomersFilter>(() => toFilter(props.report));

  const handleFilterChange = (next: CustomersFilter) => {
    setFilter(next);
    void load(next);
  };

  const handleRefresh = () => {
    void load(filter, { refresh: true });
  };

  const { totals, byCountry, topStates, topCustomers, periods } = report;
  const { pagePath } = props;
  const customersHref = toAdminHref(report.customersPath, pagePath);
  const rangeText = `${report.from} – ${report.to}`;
  const previousPeriod = formatPreviousPeriod(totals.newCustomers);
  const statusName = report.statuses.find((s) => s.id === report.orderStatusId)?.displayName;
  const statusSuffix = report.orderStatusId === null ? '' : `_status-${report.orderStatusId}`;
  const addressName = report.addressType === 'Shipping' ? 'shipping' : 'billing';

  const newSeries = useMemo(() => toValueSeries(report.newCustomers), [report.newCustomers]);
  const totalSeries = useMemo(() => toValueSeries(report.totalCustomers), [report.totalCustomers]);
  const growthSeries = useMemo(() => [newSeries, totalSeries], [newSeries, totalSeries]);
  const activeSeries = useMemo(() => toValueSeries(report.activeCustomers), [report.activeCustomers]);
  const activeTableSeries = useMemo(() => [activeSeries], [activeSeries]);

  const getCustomerHref = useCallback(
    (item: StatsRankedItem) => toAdminHref(item.adminPath, pagePath),
    [pagePath],
  );

  const noCommerce = 'Digital commerce tables were not found in this project, so there are no customers to show.';
  const ordersEmpty = !report.commerceAvailable
    ? noCommerce
    : `No customers with orders${statusName ? ` in status ${statusName}` : ''} in the selected range. Try a longer range or another status.`;

  const countryCaptions = useMemo(() => toLocationCaptions('Country', previousPeriod), [previousPeriod]);
  const stateCaptions = useMemo(() => toLocationCaptions('State or region', previousPeriod), [previousPeriod]);

  const fileSuffix = `${statusSuffix}_${addressName}_${report.from}_${report.to}`;

  const exportGrowthCsv = () => {
    saveCsv(
      'customers-growth',
      `customers-growth_${report.from}_${report.to}_${report.grouping.toLowerCase()}.csv`,
      toTimeSeriesCsv(periods, growthSeries, { includeTotal: false }),
    );
  };

  const exportActiveCsv = () => {
    // Point-in-time values per period end; no total column (the values do not add up).
    saveCsv(
      'customers-active',
      `customers-active-${report.activityWindowDays}d${statusSuffix}_${report.from}_${report.to}_${report.grouping.toLowerCase()}.csv`,
      toTimeSeriesCsv(periods, activeTableSeries, { includeTotal: false }),
    );
  };

  const exportCountriesCsv = () => {
    saveCsv('customers-by-country', `customers-by-country${fileSuffix}.csv`, toRankedCsv(byCountry.items, countryCaptions));
  };

  const exportStatesCsv = () => {
    saveCsv('customers-top-states', `customers-top-states${fileSuffix}.csv`, toRankedCsv(topStates.items, stateCaptions));
  };

  const exportTopCustomersCsv = (rankBy: TopCustomersRankBy, result: StatsRankedResult, captions: StatsRankedCaptions) => {
    const raw = (caption: string | undefined, kind: string | undefined) =>
      caption && kind === 'Amount' ? `${caption} ${rawAmountNote}` : caption;
    saveCsv(
      `customers-top-by-${rankBy}`,
      `customers-top-by-${rankBy}${statusSuffix}_${result.from}_${result.to}.csv`,
      toRankedCsv(
        result.items,
        {
          ...captions,
          value: raw(captions.value, captions.valueKind) ?? captions.value,
          secondaryValue: raw(captions.secondaryValue, captions.secondaryValueKind),
          tertiaryValue: raw(captions.tertiaryValue, captions.tertiaryValueKind),
          previousValue: raw(captions.previousValue, captions.valueKind),
        },
        getCustomerHref,
      ),
    );
  };

  const filteredHint = statusName ? ` Only orders in status ${statusName}.` : '';
  const changeHint = `Change compares with the ${previousPeriod} (${totals.newCustomers.previousFrom} – ${totals.newCustomers.previousTo}). "New" means none in that period.`;
  const locationHint = `Location is the ${addressName} address on the customer's most recent order in the range.`;

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
          customersHref && (
            <LinkButton
              label="Open customers"
              color={ButtonColor.Tertiary}
              href={customersHref}
              title="Open the Customers application"
            />
          )
        }
      >
        <OrderStatusSelect
          statuses={report.statuses}
          value={filter.orderStatusId}
          onChange={(orderStatusId) => handleFilterChange({ ...filter, orderStatusId })}
        />
        <OptionToggle
          label="Location by"
          items={addressTypeItems}
          value={filter.addressType}
          onChange={(addressType) =>
            handleFilterChange({ ...filter, addressType: addressType === 'Shipping' ? 'Shipping' : 'Billing' })
          }
        />
      </StatsFilterBar>

      <div className="AdminStats-kpis">
        <ComparisonInfoCard
          caption="New customers"
          tooltip={`Customers created in the selected range (${rangeText}), with or without orders. The order status filter does not apply.`}
          comparison={totals.newCustomers}
          noun="new customers"
        />
        <ComparisonInfoCard
          caption="Ordering customers"
          tooltip={`Customers with at least one order in the selected range.${filteredHint}`}
          comparison={totals.orderingCustomers}
          noun="ordering customers"
        />
        <ComparisonInfoCard
          caption="Returning customers"
          tooltip={`Share of ordering customers who also have an order before the range (any time). The change is in percentage points (pp).${filteredHint}`}
          comparison={totals.returningShare}
          noun="ordering customers"
        />
        <ComparisonInfoCard
          caption="Active customers"
          tooltip={`Customers with an order in the ${report.activityWindowDays} days up to the last day of the range (${report.to}), compared with the last day of the previous period.${filteredHint}`}
          comparison={totals.activeCustomers}
          noun="active customers"
        />
        <ComparisonInfoCard
          caption="Revenue per customer"
          tooltip={`Revenue (order grand totals) divided by ordering customers in the selected range.${filteredHint}`}
          comparison={totals.revenuePerCustomer}
          noun="ordering customers"
        />
      </div>

      <div className="AdminStats-tiles">
        <StatsTile
          headline="Customer growth"
          description={`New customers per ${report.grouping.toLowerCase()} (columns, left axis) and total customers at the end of each ${report.grouping.toLowerCase()} (line, right axis). Customers are counted by the date they were created; the order status filter does not apply.`}
          isLoading={isLoading}
          hasError={hasError}
          isEmpty={!report.commerceAvailable || report.totalCustomers.total === 0}
          emptyMessage={report.commerceAvailable ? 'There are no customers yet.' : noCommerce}
          onExportCsv={exportGrowthCsv}
          renderChart={() => (
            <ComboChart
              periods={periods}
              columns={newSeries}
              line={totalSeries}
              ariaLabel={`Customer growth, ${rangeText}`}
            />
          )}
          renderTable={() => (
            <TimeSeriesTable grouping={report.grouping} periods={periods} series={growthSeries} showTotal={false} />
          )}
        />

        <StatsTile
          headline="Customers by country"
          description={`Ordering customers per country. ${locationHint} Customers without that address or without a country are "Unknown". ${changeHint}${filteredHint}`}
          isLoading={isLoading}
          hasError={hasError}
          isEmpty={byCountry.items.length === 0}
          emptyMessage={ordersEmpty}
          onExportCsv={exportCountriesCsv}
          renderChart={() => (
            <RankedBarChart
              items={byCountry.items}
              captions={countryCaptions}
              ariaLabel={`Customers by country, ${rangeText}`}
            />
          )}
          renderTable={() => <RankedTable items={byCountry.items} captions={countryCaptions} />}
        />

        <StatsTile
          headline="Active customers"
          description={`Customers with at least one order in the ${report.activityWindowDays} days up to the end of each ${report.grouping.toLowerCase()} (the range end for the last one). A customer stops being active ${report.activityWindowDays} days after their last order. Values are counts on that day, so they do not add up.${filteredHint}`}
          isLoading={isLoading}
          hasError={hasError}
          isEmpty={!report.commerceAvailable || report.activeCustomers.values.every((v) => v === 0)}
          emptyMessage={report.commerceAvailable ? `No customers with orders in the ${report.activityWindowDays} days before any day of the range.` : noCommerce}
          onExportCsv={exportActiveCsv}
          headerControls={
            <NameToggleButtons
              items={activityWindowItems}
              selectedItemId={String(filter.activityWindowDays)}
              onChange={(id) => handleFilterChange({ ...filter, activityWindowDays: Number(id) })}
            />
          }
          renderChart={() => (
            <ComboChart periods={periods} line={activeSeries} ariaLabel={`Active customers, ${rangeText}`} />
          )}
          renderTable={() => (
            <TimeSeriesTable grouping={report.grouping} periods={periods} series={activeTableSeries} showTotal={false} />
          )}
        />

        <StatsTile
          headline="Top states and regions"
          description={`Ordering customers per state or region. ${locationHint} Customers without a state are left out. ${changeHint}${filteredHint}`}
          isLoading={isLoading}
          hasError={hasError}
          isEmpty={topStates.items.length === 0}
          emptyMessage={
            byCountry.items.length > 0
              ? `No ${addressName} addresses with a state in the selected range.`
              : ordersEmpty
          }
          onExportCsv={exportStatesCsv}
          renderChart={() => (
            <RankedBarChart
              items={topStates.items}
              captions={stateCaptions}
              ariaLabel={`Top states and regions, ${rangeText}`}
            />
          )}
          renderTable={() => <RankedTable items={topStates.items} captions={stateCaptions} />}
        />
      </div>

      <TopCustomersTile
        byRevenue={topCustomers.byRevenue}
        byOrders={topCustomers.byOrders}
        byQuantity={topCustomers.byQuantity}
        previousPeriod={previousPeriod}
        isLoading={isLoading}
        hasError={hasError}
        emptyMessage={ordersEmpty}
        descriptionSuffix={`${changeHint}${filteredHint}`}
        rangeText={rangeText}
        getAdminHref={getCustomerHref}
        onExportCsv={exportTopCustomersCsv}
      />

      <DataRetentionNote
        message="New customers are counted by the date the customer was created. Ordering customers have at least one order in the range; returning customers also ordered before it. Revenue is the order grand total (incl. shipping and tax) as stored, formatted by the project's price formatter and not converted between currencies. Deleted customers and orders are not counted."
        link={{ href: commerceDocsUrl, label: 'Learn how to manage commerce stores' }}
      />
    </div>
  );
};
