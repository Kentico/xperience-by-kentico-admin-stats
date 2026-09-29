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

/** Mirrors `StatsRankedItem`: one row of a ranked list. */
export interface StatsRankedItem {
  /** 1-based position. */
  readonly rank: number;
  /** Stable, unique identifier (for example the URL). */
  readonly key: string;
  readonly label: string;
  readonly secondaryLabel: string | null;
  readonly value: number;
  readonly secondaryValue: number | null;
  /** Share of `StatsRankedResult.total` (0–1). */
  readonly share: number;
  /** Absolute link opened in a new tab. */
  readonly url: string | null;
}

/** Mirrors `StatsRankedResult`: ranked list for one range and channel. */
export interface StatsRankedResult {
  readonly from: string;
  readonly to: string;
  readonly channelId: number | null;
  /** Top items, largest value first. */
  readonly items: readonly StatsRankedItem[];
  /** Sum of values over all items in the range (not only `items`). */
  readonly total: number;
  /** Number of distinct items in the range (not only `items`). */
  readonly itemCount: number;
  /** ISO timestamp of when the data was read from the database. */
  readonly updatedAt: string;
}

/** Column captions of a ranked list, used by the table, chart tooltip and CSV. */
export interface StatsRankedCaptions {
  /** Caption of the label column, for example "Page". */
  readonly label: string;
  /** Caption of the secondary label, for example "Title". Omit to hide it. */
  readonly secondaryLabel?: string;
  /** Caption of the value, for example "Visits". */
  readonly value: string;
  /** Caption of the secondary value, for example "Unique contacts". Omit to hide it. */
  readonly secondaryValue?: string;
}

/** One chart series. `values` aligns with the period axis. */
export interface StatsSeries {
  readonly key: string;
  readonly name: string;
  readonly values: readonly number[];
}
