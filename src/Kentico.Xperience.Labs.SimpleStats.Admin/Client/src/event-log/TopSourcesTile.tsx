import { NameToggleButton, NameToggleButtons } from '@kentico/xperience-admin-components';
import React, { useState } from 'react';

import { RankedBarChart } from '../shared/RankedBarChart';
import { RankedTable } from '../shared/RankedTable';
import { StatsTile } from '../shared/StatsTile';
import { StatsRankedCaptions, StatsRankedResult } from '../shared/types';

type SourceKind = 'all' | 'xperience' | 'custom';

/** Toggle items. Xperience sources start with `CMS.`, `Kentico.` or are named `WebFarmMonitor` (server list). */
const sourceKindItems: NameToggleButton[] = [
  { id: 'all', label: 'All' },
  { id: 'xperience', label: 'Xperience' },
  { id: 'custom', label: 'Custom' },
];

const sourceKindHints: Readonly<Record<SourceKind, string>> = {
  all: '',
  xperience: ' Only sources logged by Xperience (starting with CMS. or Kentico., and WebFarmMonitor).',
  custom: ' Only custom sources (all other sources), usually logged by project code.',
};

export interface TopSourcesTileProps {
  readonly all: StatsRankedResult;
  readonly xperience: StatsRankedResult;
  readonly custom: StatsRankedResult;
  readonly captions: StatsRankedCaptions;
  readonly isLoading: boolean;
  readonly hasError: boolean;
  /** Plural noun of the event type filter, for example "errors" or "events". */
  readonly noun: string;
  /** Empty message of the "All" option. */
  readonly emptyMessage: string;
  /** Text appended to the description (change and type filter hints). */
  readonly descriptionSuffix: string;
  readonly rangeText: string;
  /** Downloads the list; `name` includes the selected option (for example `sources-custom`). */
  readonly onExportCsv: (name: string, result: StatsRankedResult) => void;
}

/**
 * "Top sources" tile with an All / Xperience / Custom toggle. The toggle state lives here, so switching it
 * re-renders only this tile. All three lists come in the same response, so switching needs no request.
 */
export const TopSourcesTile = ({
  all,
  xperience,
  custom,
  captions,
  isLoading,
  hasError,
  noun,
  emptyMessage,
  descriptionSuffix,
  rangeText,
  onExportCsv,
}: TopSourcesTileProps) => {
  const [kind, setKind] = useState<SourceKind>('all');

  const sources = kind === 'xperience' ? xperience : kind === 'custom' ? custom : all;
  const kindNoun = kind === 'all' ? '' : `${kind === 'xperience' ? 'Xperience' : 'custom'} `;
  const message =
    kind === 'all'
      ? emptyMessage
      : `No ${noun} from ${kindNoun}sources in the selected range.${kind === 'custom' ? ' Custom sources appear when project code logs events.' : ''}`;

  return (
    <StatsTile
      headline="Top sources"
      description={`Sources with the most events.${sourceKindHints[kind]} ${descriptionSuffix}`}
      isLoading={isLoading}
      hasError={hasError}
      isEmpty={sources.items.length === 0}
      emptyMessage={message}
      onExportCsv={() => onExportCsv(kind === 'all' ? 'sources' : `sources-${kind}`, sources)}
      headerControls={
        <NameToggleButtons
          items={sourceKindItems}
          selectedItemId={kind}
          onChange={(id) => setKind(id as SourceKind)}
        />
      }
      renderChart={() => (
        <RankedBarChart
          items={sources.items}
          captions={captions}
          ariaLabel={`Top ${kindNoun}event sources, ${rangeText}`}
        />
      )}
      renderTable={() => <RankedTable items={sources.items} captions={captions} />}
    />
  );
};
