import { Headline, HeadlineSize, NameToggleButton, NameToggleButtons } from '@kentico/xperience-admin-components';
import React, { useMemo, useState } from 'react';

import { RankedBarChart } from '../shared/RankedBarChart';
import { RankedTable } from '../shared/RankedTable';
import { StatsTile } from '../shared/StatsTile';
import { StatsRankedCaptions, StatsRankedItem, StatsRankedResult } from '../shared/types';

export type PerformersRankBy = 'open' | 'click';

/** Mirrors `EmailSummaryPerformers`. */
export interface EmailSummaryPerformers {
  /** Highest rate first. */
  readonly top: StatsRankedResult;
  /** Lowest rate first, without the emails in `top`. */
  readonly bottom: StatsRankedResult;
}

const rankByItems: NameToggleButton[] = [
  { id: 'open', label: 'Open rate' },
  { id: 'click', label: 'Click rate' },
];

export interface PerformersTileProps {
  readonly byOpenRate: EmailSummaryPerformers;
  readonly byClickRate: EmailSummaryPerformers;
  readonly minDelivered: number;
  readonly isLoading: boolean;
  readonly hasError: boolean;
  readonly emptyMessage: string;
  readonly rangeText: string;
  /** Link to the email's Statistics tab, or `null`. */
  readonly getAdminHref: (item: StatsRankedItem) => string | null;
  /** Downloads both lists of the selected rate. */
  readonly onExportCsv: (rankBy: PerformersRankBy, performers: EmailSummaryPerformers, captions: StatsRankedCaptions) => void;
}

/**
 * "Top and bottom performers" tile with an Open rate / Click rate toggle. Both lists come in the same response, so switching needs no request.
 */
export const PerformersTile = ({
  byOpenRate,
  byClickRate,
  minDelivered,
  isLoading,
  hasError,
  emptyMessage,
  rangeText,
  getAdminHref,
  onExportCsv,
}: PerformersTileProps) => {
  const [rankBy, setRankBy] = useState<PerformersRankBy>('open');
  const performers = rankBy === 'click' ? byClickRate : byOpenRate;

  const captions = useMemo<StatsRankedCaptions>(
    () => ({
      label: 'Email',
      secondaryLabel: 'Send date',
      value: rankBy === 'click' ? 'Click rate' : 'Open rate',
      valueKind: 'Ratio',
      secondaryValue: 'Delivered',
    }),
    [rankBy],
  );
  const rateName = captions.value.toLowerCase();

  const lists = [
    { key: 'top', title: 'Top', items: performers.top.items },
    { key: 'bottom', title: 'Bottom', items: performers.bottom.items },
  ].filter((list) => list.items.length > 0);

  return (
    <StatsTile
      headline="Top and bottom performers"
      description={`Regular emails sent in the selected range with the highest and the lowest ${rateName} (unique ${rankBy === 'click' ? 'clicks' : 'opens'} divided by delivered). Emails with fewer than ${minDelivered} delivered are left out. With few emails, the bottom list leaves out the emails already in the top list.`}
      isLoading={isLoading}
      hasError={hasError}
      isEmpty={lists.length === 0}
      emptyMessage={emptyMessage}
      onExportCsv={() => onExportCsv(rankBy, performers, captions)}
      defaultView="table"
      headerControls={
        <NameToggleButtons items={rankByItems} selectedItemId={rankBy} onChange={(id) => setRankBy(id as PerformersRankBy)} />
      }
      renderChart={() => (
        <div className="SimpleStats-tileColumn">
          {lists.map((list) => (
            <div key={list.key}>
              <Headline size={HeadlineSize.S}>{`${list.title} ${list.items.length}`}</Headline>
              <RankedBarChart
                items={list.items}
                captions={captions}
                getHref={getAdminHref}
                showShare={false}
                ariaLabel={`${list.title} emails by ${rateName}, ${rangeText}`}
              />
            </div>
          ))}
        </div>
      )}
      renderTable={() => (
        <div className="SimpleStats-tileColumn">
          {lists.map((list) => (
            <div key={list.key}>
              <Headline size={HeadlineSize.S}>{`${list.title} ${list.items.length}`}</Headline>
              <RankedTable items={list.items} captions={captions} getAdminHref={getAdminHref} showSecondaryLabel showShare={false} />
            </div>
          ))}
        </div>
      )}
    />
  );
};
