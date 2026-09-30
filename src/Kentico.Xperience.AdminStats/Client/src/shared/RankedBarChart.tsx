import * as am5 from '@amcharts/amcharts5';
import am5ThemesAnimated from '@amcharts/amcharts5/themes/Animated';
import * as am5xy from '@amcharts/amcharts5/xy';
import { Colors } from '@kentico/xperience-admin-components';
import React, { useId, useLayoutEffect, useMemo } from 'react';

import { getChartTokens, getSeriesPalette, getXbkTheme, resolveToken } from './chartTheme';
import { formatShare, numberFormat } from './format';
import { StatsRankedCaptions, StatsRankedItem } from './types';

export interface RankedBarChartProps {
  readonly items: readonly StatsRankedItem[];
  readonly captions: StatsRankedCaptions;
  /** Accessible name for the chart. */
  readonly ariaLabel: string;
  /** Optional link per item. Clicking its bar opens it in the same tab. */
  readonly getHref?: (item: StatsRankedItem) => string | null;
  /** Shows the share of the total in tooltips. Hide it when values do not add up (for example days). Default `true`. */
  readonly showShare?: boolean;
  /** Bars with a value at or above this are drawn in the alert color (for example items waiting too long). */
  readonly highlightFrom?: number;
}

interface ChartRow {
  readonly key: string;
  readonly label: string;
  readonly value: number;
  readonly tooltip: string;
  readonly href: string | null;
  readonly highlight: boolean;
}

const rowHeight = 32;
const chartPadding = 48;
const minHeight = 160;

/** Share of the chart width that axis labels may use before they are truncated. */
const labelWidthRatio = 0.4;
const minLabelWidth = 120;

/** amCharts reads `[...]` as text formatting; double the brackets so data shows as typed. */
function escapeChartText(text: string): string {
  return text.replace(/\[/g, '[[').replace(/\]/g, ']]');
}

/**
 * Horizontal ranked bar chart (amCharts 5): one bar per item, largest on top,
 * value labels at bar ends. Long labels are truncated; the full text shows in the tooltip.
 * The root is created in `useLayoutEffect` and disposed on unmount or data change.
 */
export const RankedBarChart = ({
  items,
  captions,
  ariaLabel,
  getHref,
  showShare = true,
  highlightFrom,
}: RankedBarChartProps) => {
  const chartId = `stats-chart-${useId().replace(/:/g, '')}`;

  const data = useMemo<ChartRow[]>(
    () =>
      items.map((item) => {
        const lines = [
          `[bold]${escapeChartText(item.label)}[/]`,
          ...(captions.secondaryLabel && item.secondaryLabel
            ? [escapeChartText(item.secondaryLabel)]
            : []),
          `${captions.value}: ${numberFormat.format(item.value)}${showShare ? ` (${formatShare(item.share)})` : ''}`,
          ...(captions.secondaryValue && item.secondaryValue !== null
            ? [`${captions.secondaryValue}: ${numberFormat.format(item.secondaryValue)}`]
            : []),
        ];
        return {
          key: item.key,
          label: item.label,
          value: item.value,
          tooltip: lines.join('\n'),
          href: getHref?.(item) ?? null,
          highlight: highlightFrom !== undefined && item.value >= highlightFrom,
        };
      }),
    [items, captions, getHref, showShare, highlightFrom],
  );

  const height = Math.max(minHeight, data.length * rowHeight + chartPadding);

  useLayoutEffect(() => {
    const root = am5.Root.new(chartId);
    root.setThemes([am5ThemesAnimated.new(root), getXbkTheme(root)]);
    root.numberFormatter.set('numberFormat', '#,###');

    const tokens = getChartTokens();
    const barColor = getSeriesPalette()[0];
    const highlightValue = resolveToken(Colors.AlertBackgroundHighEmphasis);
    const highlightColor = highlightValue ? am5.color(highlightValue) : undefined;

    const chart = root.container.children.push(
      am5xy.XYChart.new(root, {
        panX: false,
        panY: false,
        wheelX: 'none',
        wheelY: 'none',
        layout: root.verticalLayout,
        paddingLeft: 0,
        paddingRight: 0,
      }),
    );
    chart.zoomOutButton.set('forceHidden', true);

    const createTooltip = () => {
      const tooltip = am5.Tooltip.new(root, {
        getFillFromSprite: false,
        autoTextColor: false,
        pointerOrientation: 'horizontal',
      });
      tooltip.get('background')?.setAll({
        fill: tokens.tooltip,
        stroke: tokens.tooltip,
      });
      tooltip.label.setAll({ fill: tokens.tooltipText, maxWidth: 480, oversizedBehavior: 'wrap' });
      return tooltip;
    };

    // Category axis on Y, inversed so the first (largest) item is on top.
    const yRenderer = am5xy.AxisRendererY.new(root, {
      inversed: true,
      minGridDistance: 1,
      cellStartLocation: 0.15,
      cellEndLocation: 0.85,
    });
    yRenderer.grid.template.set('visible', false);
    yRenderer.labels.template.setAll({
      fill: tokens.text,
      fontSize: 13,
      oversizedBehavior: 'truncate',
      ellipsis: '…',
      maxWidth: minLabelWidth,
      // Replaced by the adapter below; any text enables the hover tooltip.
      tooltipText: '{category}',
      tooltip: createTooltip(),
    });
    // Show the label (not the unique key) and keep the full text for the tooltip.
    yRenderer.labels.template.adapters.add('text', (text, target) => {
      const row = target.dataItem?.dataContext as ChartRow | undefined;
      return row ? escapeChartText(row.label) : text;
    });
    yRenderer.labels.template.adapters.add('tooltipText', (text, target) => {
      const row = target.dataItem?.dataContext as ChartRow | undefined;
      return row ? escapeChartText(row.label) : text;
    });

    const yAxis = chart.yAxes.push(
      am5xy.CategoryAxis.new(root, {
        categoryField: 'key',
        renderer: yRenderer,
      }),
    );
    yAxis.data.setAll(data);

    const xRenderer = am5xy.AxisRendererX.new(root, { minGridDistance: 80 });
    xRenderer.labels.template.setAll({ fill: tokens.textLow, fontSize: 12 });
    xRenderer.grid.template.setAll({ stroke: tokens.grid, strokeOpacity: 1 });

    const xAxis = chart.xAxes.push(
      am5xy.ValueAxis.new(root, {
        min: 0,
        maxPrecision: 0,
        // Room for the value labels at the bar ends.
        extraMax: 0.12,
        renderer: xRenderer,
      }),
    );

    const series = chart.series.push(
      am5xy.ColumnSeries.new(root, {
        name: captions.value,
        xAxis,
        yAxis,
        categoryYField: 'key',
        valueXField: 'value',
        tooltip: createTooltip(),
      }),
    );
    series.columns.template.setAll({
      tooltipText: '{tooltip}',
      height: am5.percent(100),
      strokeOpacity: 0,
      cornerRadiusTR: 2,
      cornerRadiusBR: 2,
      ...(barColor ? { fill: barColor } : {}),
    });

    series.bullets.push(() =>
      am5.Bullet.new(root, {
        locationX: 1,
        sprite: am5.Label.new(root, {
          text: '{valueX}',
          fill: tokens.text,
          fontSize: 12,
          centerY: am5.p50,
          centerX: am5.p0,
          dx: 6,
          populateText: true,
        }),
      }),
    );

    // Highlighted rows (see `highlightFrom`) use the alert color.
    if (highlightColor && data.some((row) => row.highlight)) {
      series.columns.template.adapters.add('fill', (fill, target) =>
        (target.dataItem?.dataContext as ChartRow | undefined)?.highlight ? highlightColor : fill,
      );
    }

    series.data.setAll(data);

    // Bars of linked items open the link, like the table's link cells.
    if (data.some((row) => row.href)) {
      const hrefOf = (target: am5.Sprite) =>
        (target.dataItem?.dataContext as ChartRow | undefined)?.href ?? null;
      series.columns.template.adapters.add('cursorOverStyle', (style, target) =>
        hrefOf(target) ? 'pointer' : style,
      );
      series.columns.template.events.on('click', (ev) => {
        const href = hrefOf(ev.target);
        if (href) {
          window.location.assign(href);
        }
      });
    }

    // Truncate labels relative to the chart width, so wide screens show more text.
    chart.events.on('boundschanged', () => {
      const maxWidth = Math.max(minLabelWidth, Math.round(chart.width() * labelWidthRatio));
      if (yRenderer.labels.template.get('maxWidth') !== maxWidth) {
        yRenderer.labels.template.set('maxWidth', maxWidth);
      }
    });

    void series.appear(600);
    void chart.appear(600, 100);

    return () => {
      root.dispose();
    };
  }, [chartId, data, captions.value]);

  return (
    <div
      id={chartId}
      role="img"
      aria-label={ariaLabel}
      className="AdminStats-chart AdminStats-chart--ranked"
      style={{ height }}
    />
  );
};
