import { NameToggleButton, NameToggleButtons } from '@kentico/xperience-admin-components';
import React from 'react';

import { ChannelSelect, RefreshControl } from './filterControls';
import { StatsChannelOption, StatsSnapshotFilter } from './types';

/** Toggle item id that stands for "all" (`kind: null`). */
export const allKindsId = 'all';

export interface SnapshotKindOptions {
  /** Label above the toggle, for example "Content type type". */
  readonly label: string;
  /** Toggle items. Use `allKindsId` for the item that clears the kind. */
  readonly items: readonly NameToggleButton[];
}

export interface SnapshotFilterBarProps {
  readonly filter: StatsSnapshotFilter;
  readonly onChange: (filter: StatsSnapshotFilter) => void;
  /** Kind toggle (for example content kinds). Omit to hide it. */
  readonly kinds?: SnapshotKindOptions;
  /** Channel options. Hide the channel filter by passing none. */
  readonly channels?: readonly StatsChannelOption[];
  /** Reloads the current filter bypassing the server cache. */
  readonly onRefresh: () => void;
  /** Shows the refresh button as in progress. */
  readonly isLoading?: boolean;
  /** ISO timestamp of when the shown data was read from the database. */
  readonly updatedAt?: string;
}

/**
 * Filters of a current-state (snapshot) report: optional kind toggle, channel, refresh.
 * Same layout as `StatsFilterBar`, without date range or grouping.
 */
export const SnapshotFilterBar = ({
  filter,
  onChange,
  kinds,
  channels = [],
  onRefresh,
  isLoading = false,
  updatedAt,
}: SnapshotFilterBarProps) => (
  <div className="AdminStats-filterBar">
    {kinds && (
      <div className="AdminStats-filterItem">
        <span className="AdminStats-label">{kinds.label}</span>
        <NameToggleButtons
          items={[...kinds.items]}
          selectedItemId={filter.kind ?? allKindsId}
          onChange={(id) => onChange({ ...filter, kind: id === allKindsId ? null : id })}
        />
      </div>
    )}

    {channels.length > 0 && (
      <ChannelSelect
        channels={channels}
        channelId={filter.channelId}
        onChange={(channelId) => onChange({ ...filter, channelId })}
      />
    )}

    <RefreshControl onRefresh={onRefresh} isLoading={isLoading} updatedAt={updatedAt} />
  </div>
);
