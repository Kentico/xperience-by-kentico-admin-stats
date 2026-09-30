# Report 07: Orders and revenue (digital commerce)

Seventh report. Audience: marketers, store managers. PLAN "Later candidates → Other → Digital commerce orders and revenue". Time-range report like 01–04, 06. Read `PLAN.md` and `REPORT-01..06-*.md` first. Build on existing code; do not duplicate. Keep complexity low: few tiles, reuse shared pieces, add only the shared infra listed below.

## Local data (checked 2026-09-30)

DB: `mssql2022` docker, DB `xperience-by-kentico-admin-stats`, creds in `examples/DancingGoat/appsettings.json`. From Git Bash: `MSYS_NO_PATHCONV=1 docker exec mssql2022 /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P ... -d xperience-by-kentico-admin-stats -W -Q "..."`. Package version 31.9.0.

- `Commerce_Order`: **0 rows**. Columns: `OrderID`, `OrderGUID`, `OrderNumber` nvarchar, `OrderCreatedWhen` datetime2 NOT NULL, `OrderModifiedWhen`, `OrderOrderStatusID` int NOT NULL, `OrderTotalPrice`/`OrderTotalShipping`/`OrderTotalTax`/`OrderGrandTotal` decimal NULL, `OrderCustomerID` int NOT NULL, `OrderPaymentMethodID` NULL, `OrderPaymentMethodDisplayName`, `OrderShippingMethodID` NULL, `OrderShippingMethodDisplayName`, `OrderShippingMethodPrice` decimal NOT NULL.
- `Commerce_OrderItem`: 0 rows. `OrderItemOrderID`, `OrderItemSKU`, `OrderItemName` (snapshots, nullable), `OrderItemQuantity`/`OrderItemUnitPrice`/`OrderItemTotalPrice` decimal NULL, `OrderItemTotalTax`, `OrderItemTaxRate` NOT NULL.
- `Commerce_OrderStatus`: 5 rows (Pending 1, Payment failed 2, Payment received 3, Processing 4, Fulfilled 5; `OrderStatusOrder` gives display order). Statuses are editable per project → never hardcode names/IDs.
- `Commerce_Customer` 0 rows (`CustomerCreatedWhen` NOT NULL, `CustomerMemberID` NULL). Payment methods 2, shipping methods 2.
- **No channel column** on orders → no channel filter; normalize channel to null for cache key.
- **No currency** stored on orders or in settings. Currency comes from the project's price formatter `CMS.Commerce.IPriceFormatter` (DancingGoat `Commerce/PriceFormatter.cs`, en-US `C2`). **Amounts must show the project's currency** (user change, see "Amount formatting" below). Say this in the usage guide.
- PLAN note: "not yet checked which commerce tables are stable enough". Verify in the `Kentico.Xperience.*` / `CMS.Commerce` assemblies (31.9) that public Info classes exist (`OrderInfo`, `OrderItemInfo`, `OrderStatusInfo`, …) and use their column-name constants / `TYPEINFO` / `IInfoProvider<>` where it keeps SQL simple. Say what you found (and any "preview/experimental" markers). Commerce may be unlicensed or unused → report must work with 0 orders, and must not fail if the tables are missing (`OBJECT_ID` check or equivalent → empty result + hint).

### Seeding (local dev DB only)

Save as `.agent-resources/seed-commerce-orders.sql`, re-runnable (delete previously seeded rows first, e.g. by an `OrderNumber` prefix like `SEED-`). Never put seeding in `src/`. Check `NOT NULL` columns and FKs first.

- ~25 customers (some linked to contacts' emails if easy; not required).
- ~300–400 orders over the last ~120 days: uneven (weekday/weekend rhythm, a promo spike, a few empty days), statuses spread (mostly Fulfilled/Payment received, some Pending/Processing, a few Payment failed), both payment + shipping methods (copy display names from method tables).
- 1–4 items per order, SKU/name from real DancingGoat products (read product content items or reuse names from `examples/DancingGoat` data), totals consistent: item total = qty × unit price; order total = sum items; grand total = total + shipping + tax.
- Addresses only if required by FKs/constraints.

## Scope

### Server (C#)

1. **Page** `OrdersRevenuePage` (name suggestion), slug `orders-revenue`, name "Orders and revenue", order 700, icon same as the native commerce Orders application (find its `UIApplication` icon in the admin assemblies, like report 06 did for Event log). Template `@kentico/xperience-admin-stats/OrdersRevenue`. Permission `StatsPermissions.ORDERS_REVENUE` = `Kentico.Xperience.AdminStats.OrdersRevenue`, "Orders and revenue", same pattern as other pages (`UIPermission` on app, `UIEvaluatePermission` on page, `PageCommand.Permission`).
2. **Filter**: `StatsFilter` range + grouping + optional **order status** (All, or one status ID). Status options come from `Commerce_OrderStatus` sorted by `OrderStatusOrder` (sent to the client as filter options, like channel options). Unknown ID → All. Same wrapping approach as report 06's event type filter (reuse the pattern; do not change 01–06 cache keys).
3. **Queries** (repository), parameterized, aggregate in SQL, one round trip, current + previous period (`StatsComparison.GetPreviousRange`), range by `OrderCreatedWhen`:
   - Daily orders count + daily revenue (`SUM(ISNULL(OrderGrandTotal,0))`).
   - Totals current vs previous: orders, revenue, average order value (revenue / orders; null when 0 orders), items sold (sum quantity).
   - Orders + revenue by status (current range; ignore status filter for this breakdown so it always shows the mix — say so in UI hint).
   - Top products by revenue: group by `OrderItemSKU` (fallback name when SKU null), `TOP N`, with quantity, revenue, previous revenue → change. Respects status filter.
   - Optional if trivial: payment methods and shipping methods share (by denormalized display name). Skip if it adds noise.
4. **Shared infra (new, reusable)** — the main point of this report:
   - **Decimal values.** Existing `StatsComparison`, `StatsTimeSeries`, `StatsDailyCount`, ranked types are `int`. Add decimal support without changing 01–06 JSON output or tests. Pick the simplest clean option (e.g. `StatsDecimalComparison` / `StatsAmountDailyValue` + `BuildFixed` overload, or generic math `INumber<T>` if it stays readable). Say why. Unit tests incl. rounding (round money to 2 decimals server side).
   - **Value kind for the client** (count vs amount vs ratio) so shared client components format correctly. Reusable later for email summary rates.
   - Ranked items with a secondary value (revenue + quantity) — reuse report 06 ranked + change; extend opt-in only if needed.
5. **Admin links** via `IStatsAdminLinks` only: link to native Orders listing; per-status link to a filtered listing only if a public API supports it. Skip + say why if not.
6. **Result** (suggested): `OrdersRevenueResult { From, To, Grouping, OrderStatusId, Periods, Orders (series), Revenue (series), Totals { Orders, Revenue, AverageOrderValue, ItemsSold } (comparisons), ByStatus, TopProducts, OrdersAppUrl, UpdatedAt }`.
7. **Service**: interface + `StatsCache`, same rules (5 min, refresh drops key, key by normalized filter). Builder + service + SQL tests (0 orders, tables missing, one status, null totals, AOV null, previous 0 → change null). 01–06 tests still pass.

### Client (React/TS)

- Template `orders-revenue/OrdersRevenueTemplate.tsx`, export in `entry.tsx`.
- Filter bar: range + grouping + status select (reuse `filterControls.tsx` / event log type filter pattern; generalize in `shared/` if needed), refresh, updated at.
- KPI row: Orders, Revenue, Average order value, Items sold — each with change vs previous period (reuse `ComparisonInfoCard`; extend for amount formatting).
- Tile "Orders and revenue over time": **new shared `ComboChart`** (columns = orders on left axis, line = revenue on right axis, amCharts 5 from the admin) / table toggle (`TimeSeriesTable` with both series, amount column formatted), CSV. Reusable later (email sends + open rate).
- Tile "Orders by status": `DonutChart` / `ShareTable`, show both orders and revenue in table, CSV.
- Tile "Top products": `RankedBarChart` (revenue) / `RankedTable` with quantity, revenue, change columns, CSV. Full-width row.
- Shared `format.ts`: `formatValue(value, kind, text?)` prefers the server-formatted text; fallback amount format is 2 decimals, locale grouping, no symbol. CSV exports raw numbers (dot decimal, no grouping); amount headers say "(raw amount)".
- Link/button to native Orders app if the server gives a path.
- `DataRetentionNote`/hint: revenue = order grand total (incl. shipping and tax) as stored at order time; formatted by the project price formatter, not converted between currencies; deleted orders disappear.
- Empty state when commerce has no orders (or tables missing): friendly message, no errors.

## Look and feel — must feel native

Same rules as earlier reports. References: `../xperience-by-kentico-admin-design-components` (`AGENTS.md`, `src/components/Charts/*`, `src/components/Table`, `src/templates/DashboardTemplate`, `previews/*.png`, `src/styles/tokens.css`), `../community-portal/src/Kentico.Community.Portal.Admin/Client/src/features/reports/` (amCharts examples only, not a spec). Prefer `@kentico/xperience-admin-components` exports; verify props in `node_modules/@kentico/xperience-admin-components/dist/*.d.ts`. CSS only via design tokens; colors from `chartTheme.ts`. Keep admin `Table` cells plain single-line (earlier CSS overrides looked wrong). Charts `React.memo` + content-stable data (report 06 round 4) so toggles don't rebuild other charts.

## Goal

Shareable across customer projects: nothing DancingGoat-specific in `src/`, no hardcoded status names/IDs/URLs/currency, parameterized SQL, works with 0 orders, missing commerce tables, custom statuses.

## Done when

- `npm run typecheck` + `npm run build` pass in `src/Kentico.Xperience.AdminStats/Client`.
- `dotnet build` of `Kentico.Xperience.AdminStats.slnx` + `dotnet test` pass; new tests.
- DancingGoat admin: nav shows 7 reports; report renders seeded data; status filter, toggles, CSV, links work; reports 01–06 unchanged. Admin client uses Proxy mode (port 3009): `npm run watch` in Client must run, or rebuild + restart for client changes. Visual check if the app can be run (`docs/Contributing-Setup.md`, `.vscode/tasks.json`); otherwise say not checked.
- `docs/Usage-Guide.md`: report section + permission table row.
- Do not commit. Report: files changed, SQL shape, commerce API/table stability findings, decimal infra decision, filter decision, admin link approach (or why skipped), seeding done, what was / was not visually checked.

## Amount formatting (user change)

Amounts must show the project's currency, not plain decimals.

- Server: shared `IStatsAmountFormatter` (`Shared/StatsAmountFormatter.cs`) wraps the project's `IPriceFormatter` (last registered; resolved as `IEnumerable<IPriceFormatter>`, so none registered is fine) and calls it like the native Orders listing: `Format(price, new PriceFormatContext())` (`PriceFormatContext` has no members in 31.9). The product registers a default `PriceFormatter` (`price.ToString("F2")`, no symbol) via `RegisterImplementation`; projects replace it. Missing / throwing / empty formatter → `null` text → client formats a plain 2-decimal number.
- Raw decimal + formatted string for every amount, in shared types (texts are opt-in, left out of JSON when `null`, so reports 01–06 are unchanged): `StatsValueComparison.CurrentText/PreviousText`, `StatsValueSeries.Texts/TotalText`, `StatsRankedItem.ValueText/SecondaryValueText/PreviousValueText`, `StatsRankedResult.TotalText`. `StatsAmountTexts.WithTexts(...)` fills them for `Amount` kinds only; applied in the service after the cache.
- Client: KPI cards, combo chart tooltip, time series table, ranked table/chart (tooltip + bar-end labels), share table use the texts.
- Axis labels: plain numbers (`#,###.##`). Deriving a currency prefix/suffix from a formatted sample is locale-fragile, so axes stay plain and tooltips/tables show formatted amounts.
- CSV: raw numbers; amount column headers note "(raw amount)".
- Tests: formatter present / absent / failing, amounts only get texts, JSON unchanged without amounts.

## Round 2 (user feedback)

Screenshot `docs/images/dancing-goat-orders-and-revenue-stats.jpg`.

1. **ComboChart line color**: without a fixed color, the line takes the palette color (`chartTheme.ts` tokens) with the largest RGB distance from the columns' color (`getContrastingColor`). No new colors.
2. **Legend markers match the series**: the line's bullets are added after `legend.data.setAll`, so the legend marker is the plain solid line (series color, `strokeWidth` 2). The white-ringed bullet on the marker made it look dashed. Columns, stacks, slices and bars keep square markers that match their fills.
3. **amCharts logo**: every shared chart (StackedColumn, RankedBar, Donut, CoverageBar, Combo) creates its root with `createChartRoot` in `chartTheme.ts` (themes + `paddingBottom: chartLogoSpace` = 28px). The logo sits bottom-left in the root, so it no longer overlaps legends or axis labels. Bar charts with computed heights add the same space. A later amCharts license (`am5.addLicense`) goes in that one helper (code comment there). No license config was added.

Donut colors and filter bar alignment are unchanged.
