import * as am5 from '@amcharts/amcharts5';
import * as am5percent from '@amcharts/amcharts5/percent';
import React, { useId, useLayoutEffect, useMemo } from 'react';

import { createChartRoot, getChartTokens, getSeriesPalette } from './chartTheme';
import { StatsShareSlice } from './types';
import { useStableValue } from './useStableValue';

export interface DonutChartProps {
  /** Slices in display order. Colors follow the series palette in this order. */
  readonly slices: readonly StatsShareSlice[];
  /** Big text in the center, for example a share or a total. */
  readonly centerValue?: string;
  /** Small text under `centerValue`. */
  readonly centerCaption?: string;
  /** Accessible name for the chart. */
  readonly ariaLabel: string;
}

interface ChartRow {
  readonly category: string;
  readonly value: number;
  readonly fill?: am5.Color;
}

/** amCharts reads `[...]` as text formatting; double the brackets so data shows as typed. */
function escapeChartText(text: string): string {
  return text.replace(/\[/g, '[[').replace(/\]/g, ']]');
}

/**
 * Donut chart (amCharts 5 percent pie with an inner radius): one slice per item,
 * center label, legend with shares, tooltip with value and share.
 * Slice colors follow the series palette in order, so they match `StackedColumnChart` series colors.
 * The root is created in `useLayoutEffect` and disposed on unmount or data change.
 */
export const DonutChart = React.memo(function DonutChart({
  slices,
  centerValue,
  centerCaption,
  ariaLabel,
}: DonutChartProps) {
  const chartId = `stats-chart-${useId().replace(/:/g, '')}`;

  const rows = useMemo<ChartRow[]>(() => {
    const palette = getSeriesPalette();
    return slices.map((slice, index) => ({
      category: escapeChartText(slice.name),
      value: slice.value,
      ...(palette.length > 0 ? { fill: palette[index % palette.length] } : {}),
    }));
  }, [slices]);
  // Rebuild the chart only when the rows change by content, not on every new prop identity.
  const data = useStableValue(rows);

  useLayoutEffect(() => {
    const root = createChartRoot(chartId);
    root.numberFormatter.set('numberFormat', '#,##0');

    const tokens = getChartTokens();

    const chart = root.container.children.push(
      am5percent.PieChart.new(root, {
        layout: root.verticalLayout,
        innerRadius: am5.percent(62),
        radius: am5.percent(90),
      }),
    );

    const tooltip = am5.Tooltip.new(root, {
      getFillFromSprite: false,
      autoTextColor: false,
    });
    tooltip.get('background')?.setAll({
      fill: tokens.tooltip,
      stroke: tokens.tooltip,
    });
    tooltip.label.set('fill', tokens.tooltipText);

    const series = chart.series.push(
      am5percent.PieSeries.new(root, {
        categoryField: 'category',
        valueField: 'value',
        fillField: 'fill',
        alignLabels: false,
        tooltip,
        legendValueText: "{valuePercentTotal.formatNumber('0.0')}%",
      }),
    );
    series.labels.template.set('forceHidden', true);
    series.ticks.template.set('forceHidden', true);
    series.slices.template.setAll({
      tooltipText: "[bold]{category}[/]\n{value} ({valuePercentTotal.formatNumber('0.0')}%)",
      strokeWidth: 2,
      stroke: tokens.surface,
      // Slices do not pull out on click; the chart is read-only.
      toggleKey: 'none',
    });
    series.data.setAll(data);

    if (centerValue) {
      series.children.push(
        am5.Label.new(root, {
          text: `[fontSize:24px bold]${escapeChartText(centerValue)}[/]${
            centerCaption ? `\n[fontSize:13px]${escapeChartText(centerCaption)}[/]` : ''
          }`,
          fill: tokens.text,
          textAlign: 'center',
          centerX: am5.p50,
          centerY: am5.p50,
          populateText: false,
        }),
      );
    }

    const legend = chart.children.push(
      am5.Legend.new(root, {
        centerX: am5.percent(50),
        x: am5.percent(50),
        marginTop: 16,
        layout: root.gridLayout,
      }),
    );
    legend.labels.template.setAll({ fill: tokens.text, fontSize: 13 });
    legend.valueLabels.template.setAll({ fill: tokens.textLow, fontSize: 13 });
    legend.data.setAll(series.dataItems);

    void series.appear(600, 100);

    return () => {
      root.dispose();
    };
  }, [chartId, data, centerValue, centerCaption]);

  return (
    <div
      id={chartId}
      role="img"
      aria-label={ariaLabel}
      className="SimpleStats-chart SimpleStats-chart--donut"
    />
  );
});
