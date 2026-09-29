import * as am5 from '@amcharts/amcharts5';
import { Colors } from '@kentico/xperience-admin-components';

/**
 * Same font rule as the admin design system chart theme
 * (xperience-by-kentico-admin-design-components `Charts/ChartTheme.ts`).
 */
export function getXbkTheme(root: am5.Root): am5.Theme {
  const theme = am5.Theme.new(root);
  theme.rule('Label').setAll({
    fontFamily: 'GT Walsheim, sans-serif',
  });
  return theme;
}

/**
 * Resolves a `Colors` token (`var(--name)`) to its current value.
 * amCharts draws on canvas, so it cannot use CSS custom properties directly.
 */
export function resolveToken(token: Colors): string | undefined {
  const name = /var\((--[^)]+)\)/.exec(token)?.[1];
  if (!name) {
    return undefined;
  }
  const value = getComputedStyle(document.documentElement)
    .getPropertyValue(name)
    .trim();
  return value.startsWith('#') || value.startsWith('rgb') ? value : undefined;
}

/** Solid admin color tokens used as the series palette, in order. */
const paletteTokens: Colors[] = [
  Colors.Product,
  Colors.InfoBackgroundHighEmphasis,
  Colors.SuccessBackgroundHighEmphasis,
  Colors.WarningBackgroundHighEmphasis,
  Colors.AlertBackgroundHighEmphasis,
  Colors.ProductSelectedHover,
  Colors.WarningIcon,
  Colors.TextLowEmphasis,
];

export function getSeriesPalette(): am5.Color[] {
  return paletteTokens
    .map(resolveToken)
    .filter((value): value is string => value !== undefined)
    .map((value) => am5.color(value));
}

export interface ChartTokens {
  readonly text: am5.Color;
  readonly textLow: am5.Color;
  readonly grid: am5.Color;
  readonly tooltip: am5.Color;
  readonly tooltipText: am5.Color;
}

export function getChartTokens(): ChartTokens {
  const read = (token: Colors, fallback: string) =>
    am5.color(resolveToken(token) ?? fallback);

  // Fallbacks match tokens.css values in case a token is missing at runtime.
  return {
    text: read(Colors.TextDefaultOnLight, '#151515'),
    textLow: read(Colors.TextLowEmphasis, '#525252'),
    grid: read(Colors.DividerDefault, '#dfdfdf'),
    tooltip: read(Colors.TooltipBackground, '#151515'),
    tooltipText: read(Colors.TextDefaultOnDark, '#ffffff'),
  };
}
