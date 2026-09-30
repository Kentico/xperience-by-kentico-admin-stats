import {
  Button,
  ButtonColor,
  DateTimeRangeInput,
  MenuItem,
  NameToggleButton,
  NameToggleButtons,
  Select,
} from '@kentico/xperience-admin-components';
import React from 'react';

import { addDays, formatDateOnly, parseDateOnly, rangeLength } from './dates';
import { StatsChannelOption, StatsFilter, StatsGrouping } from './types';

const presetDays = [7, 30, 90] as const;
const customPresetId = 'custom';
const allChannelsValue = '0';

const presetItems: NameToggleButton[] = [
  ...presetDays.map((days) => ({ id: String(days), label: `${days} days` })),
  { id: customPresetId, label: 'Custom' },
];

const groupingItems: NameToggleButton[] = [
  { id: 'Day', label: 'Day' },
  { id: 'Week', label: 'Week' },
  { id: 'Month', label: 'Month' },
];

export interface StatsFilterBarProps {
  readonly filter: StatsFilter;
  /** Server date (`yyyy-MM-dd`) that presets end on. */
  readonly today: string;
  /** Channel options. Hide the channel filter by passing none. */
  readonly channels?: readonly StatsChannelOption[];
  readonly onChange: (filter: StatsFilter) => void;
  /** Reloads the current filter bypassing the server cache. Hide the button by omitting it. */
  readonly onRefresh?: () => void;
  /** Shows the refresh button as in progress. */
  readonly isLoading?: boolean;
  /** ISO timestamp of when the shown data was read from the database. */
  readonly updatedAt?: string;
  /** Shows the grouping control. Hide it for reports that use the range only (for example ranked lists). */
  readonly showGrouping?: boolean;
  /** Shows the channel filter (when there are channel options). Hide it for data without a channel (for example contacts). */
  readonly showChannel?: boolean;
}

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

function getPresetId(filter: StatsFilter, today: string): string {
  const days = rangeLength(filter.from, filter.to);
  return filter.to === today && presetDays.some((d) => d === days)
    ? String(days)
    : customPresetId;
}

/** Shared filters: date range presets + custom range, grouping, channel, refresh. */
export const StatsFilterBar = ({
  filter,
  today,
  channels = [],
  onChange,
  onRefresh,
  isLoading = false,
  updatedAt,
  showGrouping = true,
  showChannel = true,
}: StatsFilterBarProps) => {
  const updatedText = updatedAt ? formatUpdatedAt(updatedAt) : null;

  const [showCustom, setShowCustom] = React.useState(
    () => getPresetId(filter, today) === customPresetId,
  );
  const presetId = showCustom ? customPresetId : getPresetId(filter, today);

  const handlePreset = (id: string) => {
    if (id === customPresetId) {
      setShowCustom(true);
      return;
    }
    setShowCustom(false);
    onChange({ ...filter, from: addDays(today, -(Number(id) - 1)), to: today });
  };

  const handleRange = (value: { from: Date; to: Date } | null) => {
    if (!value) {
      return;
    }
    const from = formatDateOnly(value.from);
    const to = formatDateOnly(value.to);
    if (from !== filter.from || to !== filter.to) {
      onChange({ ...filter, from, to });
    }
  };

  const handleChannel = (value?: string) => {
    const id = Number(value ?? allChannelsValue);
    onChange({ ...filter, channelId: id > 0 ? id : null });
  };

  return (
    <div className="AdminStats-filterBar">
      <div className="AdminStats-filterItem">
        <span className="AdminStats-label">Date range</span>
        <NameToggleButtons
          items={presetItems}
          selectedItemId={presetId}
          onChange={handlePreset}
        />
      </div>

      {showCustom && (
        <div className="AdminStats-filterItem">
          <span className="AdminStats-label">From – to</span>
          <DateTimeRangeInput
            value={{
              from: parseDateOnly(filter.from),
              to: parseDateOnly(filter.to),
            }}
            maxDate={parseDateOnly(today)}
            showTime={false}
            onChange={handleRange}
          />
        </div>
      )}

      {showGrouping && (
        <div className="AdminStats-filterItem">
          <span className="AdminStats-label">Group by</span>
          <NameToggleButtons
            items={groupingItems}
            selectedItemId={filter.grouping}
            onChange={(id) => onChange({ ...filter, grouping: id as StatsGrouping })}
          />
        </div>
      )}

      {showChannel && channels.length > 0 && (
        <div className="AdminStats-filterItem AdminStats-filterItem--channel">
          <Select
            label="Channel"
            value={String(filter.channelId ?? allChannelsValue)}
            onChange={handleChannel}
          >
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
      )}

      {onRefresh && (
        <div className="AdminStats-filterItem AdminStats-filterItem--refresh">
          {updatedText && (
            <span className="AdminStats-updated">Updated {updatedText}</span>
          )}
          <Button
            label="Refresh"
            icon="xp-rotate-right"
            color={ButtonColor.Secondary}
            title="Reload data from the database"
            inProgress={isLoading}
            onClick={onRefresh}
          />
        </div>
      )}
    </div>
  );
};
