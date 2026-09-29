/** Mirrors `Kentico.Xperience.AdminStats.Shared.StatsGrouping`. */
export type StatsGrouping = 'Day' | 'Week' | 'Month';

/** Mirrors `StatsFilter`. Dates are `yyyy-MM-dd` (server date, no time zone). */
export interface StatsFilter {
  readonly from: string;
  readonly to: string;
  readonly grouping: StatsGrouping;
  readonly channelId: number | null;
}

/** Mirrors `StatsLoadRequest` (input of the `LOAD` page command). */
export interface StatsLoadRequest {
  readonly filter: StatsFilter;
  /** Drop cached data for the filter and read it again from the database. */
  readonly refresh: boolean;
}

/** Mirrors `StatsPeriod`. */
export interface StatsPeriod {
  readonly start: string;
  readonly label: string;
}

/** Mirrors `StatsChannelOption`. */
export interface StatsChannelOption {
  readonly id: number;
  readonly displayName: string;
  readonly type: string;
}

/** One chart series. `values` aligns with the period axis. */
export interface StatsSeries {
  readonly key: string;
  readonly name: string;
  readonly values: readonly number[];
}
