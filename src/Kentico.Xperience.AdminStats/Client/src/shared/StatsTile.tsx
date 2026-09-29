import {
  Button,
  ButtonColor,
  ButtonSize,
  Card,
  Headline,
  HeadlineSize,
  IconToggleButtons,
  Spinner,
} from '@kentico/xperience-admin-components';
import React, { ReactNode, useState } from 'react';

export type StatsTileView = 'chart' | 'table';

export interface StatsTileProps {
  readonly headline: string;
  readonly description?: string;
  readonly isLoading?: boolean;
  readonly hasError?: boolean;
  readonly isEmpty?: boolean;
  readonly emptyMessage?: string;
  readonly renderChart: () => ReactNode;
  readonly renderTable: () => ReactNode;
  /** Called by the CSV button. Omit to hide the button. */
  readonly onExportCsv?: () => void;
}

const viewItems = [
  { id: 'chart', icon: 'xp-graph' as const, tooltip: 'Chart', ariaLabel: 'Show chart' },
  { id: 'table', icon: 'xp-table' as const, tooltip: 'Table', ariaLabel: 'Show table' },
];

/** Report tile: headline, chart/table toggle, CSV export, loading, empty and error states. */
export const StatsTile = ({
  headline,
  description,
  isLoading = false,
  hasError = false,
  isEmpty = false,
  emptyMessage = 'No data for the selected filters.',
  renderChart,
  renderTable,
  onExportCsv,
}: StatsTileProps) => {
  const [view, setView] = useState<StatsTileView>('chart');

  let content: ReactNode;
  if (hasError) {
    content = (
      <div className="AdminStats-error">
        The report could not be loaded. Try again or change the filters.
      </div>
    );
  } else if (isEmpty) {
    content = <div className="AdminStats-empty">{emptyMessage}</div>;
  } else {
    content = view === 'chart' ? renderChart() : renderTable();
  }

  return (
    <Card
      headline={
        <div className="AdminStats-tileHeader">
          <Headline size={HeadlineSize.M}>{headline}</Headline>
          <div className="AdminStats-tileActions">
            <IconToggleButtons
              items={viewItems}
              selectedItemId={view}
              onChange={(id) => setView(id as StatsTileView)}
            />
            {onExportCsv && (
              <Button
                label="Export CSV"
                icon="xp-arrow-down-line"
                color={ButtonColor.Secondary}
                size={ButtonSize.S}
                disabled={isLoading || isEmpty || hasError}
                onClick={onExportCsv}
              />
            )}
          </div>
        </div>
      }
      description={description}
    >
      <div
        className={`AdminStats-tileBody${isLoading ? ' AdminStats-tileBody--loading' : ''}`}
        aria-busy={isLoading}
      >
        {isLoading && (
          <div className="AdminStats-loading">
            <Spinner />
          </div>
        )}
        {content}
      </div>
    </Card>
  );
};
