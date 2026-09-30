import { Button, ButtonColor, MenuItem, Select } from '@kentico/xperience-admin-components';
import React from 'react';

import { StatsChannelOption } from './types';

const allChannelsValue = '0';

const timeFormat = new Intl.DateTimeFormat(undefined, { timeStyle: 'short' });
const dateTimeFormat = new Intl.DateTimeFormat(undefined, {
  dateStyle: 'medium',
  timeStyle: 'short',
});

function formatUpdatedAt(value: string): string | null {
  const date = new Date(value);
  if (Number.isNaN(date.getTime()) || date.getFullYear() < 2000) {
    return null;
  }
  const isToday = date.toDateString() === new Date().toDateString();
  return (isToday ? timeFormat : dateTimeFormat).format(date);
}

export interface ChannelSelectProps {
  readonly channels: readonly StatsChannelOption[];
  /** Selected channel ID, `null` for all channels. */
  readonly channelId: number | null;
  readonly onChange: (channelId: number | null) => void;
}

/** Channel filter item of the filter bars: "All channels" plus one option per channel. */
export const ChannelSelect = ({ channels, channelId, onChange }: ChannelSelectProps) => {
  const handleChange = (value?: string) => {
    const id = Number(value ?? allChannelsValue);
    onChange(id > 0 ? id : null);
  };

  return (
    <div className="AdminStats-filterItem AdminStats-filterItem--channel">
      <Select label="Channel" value={String(channelId ?? allChannelsValue)} onChange={handleChange}>
        <MenuItem primaryLabel="All channels" value={allChannelsValue} />
        {channels.map((channel) => (
          <MenuItem
            key={channel.id}
            primaryLabel={channel.displayName}
            secondaryLabel={channel.type}
            value={String(channel.id)}
          />
        ))}
      </Select>
    </div>
  );
};

export interface RefreshControlProps {
  /** Reloads the current filter bypassing the server cache. */
  readonly onRefresh: () => void;
  /** Shows the refresh button as in progress. */
  readonly isLoading: boolean;
  /** ISO timestamp of when the shown data was read from the database. */
  readonly updatedAt?: string;
}

/** Filter bar item at the end of the bar: "Updated <time>" and the refresh button. */
export const RefreshControl = ({ onRefresh, isLoading, updatedAt }: RefreshControlProps) => {
  const updatedText = updatedAt ? formatUpdatedAt(updatedAt) : null;

  return (
    <div className="AdminStats-filterItem AdminStats-filterItem--refresh">
      {updatedText && <span className="AdminStats-updated">Updated {updatedText}</span>}
      <Button
        label="Refresh"
        icon="xp-rotate-right"
        color={ButtonColor.Secondary}
        title="Reload data from the database"
        inProgress={isLoading}
        onClick={onRefresh}
      />
    </div>
  );
};
