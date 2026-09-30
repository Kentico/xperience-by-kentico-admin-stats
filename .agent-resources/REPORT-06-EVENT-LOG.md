# Report 06: Event log

Sixth report. Audience: administrators and developers (PLAN "Later candidates → Event log"). Time-range report like 01–04. Read `PLAN.md` (Event log section + permission notes) and `REPORT-01..05-*.md` first. Build on existing code; do not duplicate.

## Local data (checked 2026-09-29)

DB: `mssql2022` docker, DB `xperience-by-kentico-admin-stats`, creds in `examples/DancingGoat/appsettings.json`. From Git Bash: `MSYS_NO_PATHCONV=1 docker exec mssql2022 /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P ... -d xperience-by-kentico-admin-stats -W -Q "..."`.

- `CMS_EventLog`: 918 rows, all on 2026-09-29 (I 832, W 74, E 12). ~28 sources, ~24 codes, 2 users. Top source `CMS.ContentEngine.ContentItemAssetVariantCacheCleaner`.
- Setting `CMSLogSize` = 10000 (event log size limit; verify meaning = max rows kept, and whether 0 = unlimited, via kentico-docs MCP). Use it in the retention note (read the value, don't hardcode).
- Trends need seeded data. Seed **local dev DB only**: insert copies of existing rows with `EventTime` spread over the last ~120 days (uneven, some error spikes, a few days with no events; keep some system events with no user). Stay under `CMSLogSize` or the product may trim. Check `NOT NULL` columns first. Save as `.agent-resources/seed-event-log.sql`. Never put seeding in `src/`.

## Uncommitted work in progress (keep it)

Per-report permissions are done but uncommitted: `Admin/StatsPermissions.cs`, `[UIPermission]` on `StatsApplicationPage`, `[UIEvaluatePermission]` + `PageCommand.Permission` on each page, `docs/Usage-Guide.md` permission table. Add `EVENT_LOG` (`Kentico.Xperience.AdminStats.EventLog`, "Event log") the same way. Do not revert or rework the other pages.

## Scope

### Server (C#)

1. **Page** `EventLogPage`, slug `event-log`, name "Event log", order 600, icon (verify in `Icons`), template `@kentico/xperience-admin-stats/EventLog`. Permission `StatsPermissions.EVENT_LOG`. No channel → normalize channel to null for the cache key.
2. **Filter**: reuse `StatsFilter` range + grouping. Add event type filter (All / Information / Warning / Error). Pick the cleanest way without changing reports 01–05 behavior or cache keys: e.g. an `EventLogFilter`/load request that wraps `StatsFilter` + `EventType?`, normalized (unknown → all). Say why.
3. **Queries** (repository), parameterized SQL, aggregate in SQL, one round trip (multiple result sets OK), current + previous period (`StatsComparison.GetPreviousRange`):
   - Daily counts by `EventType` in range → time series (fixed series I/W/E in stable order, stable colors: error/warning/info). Reuse `StatsTimeSeriesBuilder.BuildFixed` / shared time series.
   - Totals per type, current vs previous → `StatsComparison` per type.
   - Top sources (`TOP N`, with previous period count → change), top event codes (same). Respects type filter.
   - Top users by event count; system events (no `UserID` / empty `UserName`) as their own "System" row. Respects type filter.
   - Verify column names/types/nullability (`EventType` char? `EventTime` datetime2? `UserID` nullable?). Map type codes via a product constant/enum if one exists (e.g. `EventType` class in `CMS.Core`); say what you found.
   - Add an index note only if relevant (don't create indexes). Cap range via `StatsFilter.MaxRangeDays` as usual.
4. **Shared ranked + change**: sources/codes need "change vs previous". Check if anything fits; else add an opt-in field on `StatsRankedEntry`/`StatsRankedItem` (e.g. `PreviousValue` + `Change`) or a small shared helper, without changing reports 01–05 output. Reusable later (email summary top/bottom performers). Unit tests.
5. **Admin links** via `IStatsAdminLinks` only: link to the native Event log application (and, if simple, to a filtered listing or an event detail). If no public page type is usable, skip and say why. Optional: user row → user edit page if simple.
6. **Result** (suggested): `EventLogResult { From, To, Grouping, EventType, Periods, Series, Totals (per type comparison), TopSources (ranked+change), TopCodes (ranked+change), TopUsers (ranked), Total, LogSizeLimit, UpdatedAt }`.
7. **Service**: interface + `StatsCache`, same rules (5 min, refresh drops key, key by normalized filter). Builder + service tests (empty log, only system events, type filter, previous period math, change null when previous 0). Reports 01–05 tests still pass.

### Client (React/TS)

- Template `event-log/EventLogTemplate.tsx`, export in `entry.tsx`.
- Filter bar: range + grouping + event type toggle (reuse the shared option-toggle from report 05 if it fits; else generalize it in `shared/`), refresh, updated at.
- KPI row: total events, errors, warnings, information — each with change vs previous period (e.g. "Errors +40% vs previous 30 days"). For errors/warnings an increase is bad: show direction neutrally or with a semantic color if the design system has one; don't invent colors. Put a small reusable KPI-with-change piece in `shared/` if none exists.
- Tile "Events over time": `StackedColumnChart` (I/W/E stacked, fixed colors from palette/tokens: error, warning, info) / `TimeSeriesTable`, CSV.
- Tile "Top sources" and "Top event codes": `RankedBarChart` / `RankedTable` with a change column, CSV. Each on its own full-width row (user feedback: side by side too dense).
- Tile "Top users": `RankedTable` (System row included), CSV.
- Link/button to the native Event log app if the server gives a path.
- `DataRetentionNote`: event log is trimmed at `CMSLogSize` rows (value from server), so old events disappear and long-range trends drop.

## Look and feel — must feel native

Same rules as earlier reports. References: `../xperience-by-kentico-admin-design-components` (`AGENTS.md`, `src/components/Charts/*`, `src/components/Table`, `src/templates/DashboardTemplate`, `previews/*.png`, `src/styles/tokens.css`), `../community-portal/src/Kentico.Community.Portal.Admin/Client/src/features/reports/` (amCharts examples only, not a spec). Prefer `@kentico/xperience-admin-components` exports; verify props in `node_modules/@kentico/xperience-admin-components/dist/*.d.ts`. CSS only via design tokens. Keep admin `Table` cells plain single-line (earlier CSS overrides looked wrong). Tile layout like other reports.

## Goal

Shareable across customer projects: nothing DancingGoat-specific in `src/`, no hardcoded IDs/URLs/source names, values parameterized, works with an empty log, only system events, one type.

## Done when

- `npm run typecheck` + `npm run build` pass in `src/Kentico.Xperience.AdminStats/Client`.
- `dotnet build` of `Kentico.Xperience.AdminStats.slnx` + `dotnet test` pass; new tests.
- DancingGoat admin: nav shows 6 reports; event log report renders seeded data; type filter, toggles, CSV, links work; reports 01–05 unchanged. Admin client uses Proxy mode (port 3009): `npm run watch` in Client must run, or rebuild + restart for client changes. Visual check if the app can be run (`docs/Contributing-Setup.md`, `.vscode/tasks.json`); otherwise say not checked.
- `docs/Usage-Guide.md`: report section + permission table row.
- Do not commit. Report: files changed, SQL shape, column/constant findings, filter/request decision, ranked-change approach, admin link approach (or why skipped), seeding done, what was / was not visually checked.

## Round 2 (user feedback)

1. **Top users: change vs previous period.** Same SQL shape as sources/codes (previous count + `Change`, "New" when previous is 0). Update tests + CSV.
2. **Stack order in "Events over time":** bottom to top = Information, Warning, Error (most common at the bottom). Colors stay fixed. Legend, table and CSV series order match.
3. **Page icon:** same as the native Event log application. Find the icon the product uses (the Event log app's `UIApplication` attribute / metadata in the `Kentico.Xperience.Admin.*` assemblies) and use the same `Icons` constant.

Also (round 1 feedback): "Top sources" and "Top event codes" each on their own full-width row at any screen width (no side-by-side).

## Round 3 (user feedback)

Most top sources are native `CMS.*` sources (for example `CMS.ContentEngine.ContentItemAssetVariantCacheCleaner`) and crowd the list.

1. **Source toggle on the "Top sources" tile:** All / Xperience / Custom. Tile-level (in the tile header next to the chart/table toggle), not a page filter; affects only Top sources.
2. **Xperience sources** = sources starting with `CMS.` or `Kentico.` (any `Kentico.*`; one server constant list). Everything else is custom (for example `Microsoft.*` and `WebFarmMonitor` in the local DB; `KenticoFoo` or `MyCompany.Kentico` are custom too). Must work for customer projects: custom sources rarely start with these prefixes.
3. All three lists come in the same SQL batch (TOP N each, current + previous counts, total count), so switching is instant and the cache key does not change. Prefix match with parameterized `LIKE` patterns (wildcards escaped).
4. Empty state per option (for example no custom sources yet). CSV exports the selected list and names the option in the file name.
5. Tests for classification (incl. `Kentico.Xperience.*` and other `Kentico.*` sources), `LIKE` escaping, SQL and builder. Usage guide updated.

## Round 4 (user feedback)

1. **`WebFarmMonitor` is an Xperience source**, as an exact source name (not a prefix). SQL: `[Source] = @SourceExactN` combined with the prefix `LIKE`s; C# `IsXperience` uses the same rule (case-insensitive). Tests: `WebFarmMonitor` native; `WebFarmMonitorX` and `My.WebFarmMonitor` custom.
2. **Bug: switching the Top sources toggle also re-rendered (rebuilt) the Top event codes chart.** Fix in shared code: keep the toggle state local to the sources tile (own component), and make the shared charts (`RankedBarChart`, `StackedColumnChart`, `DonutChart`, `CoverageBarChart`) rebuild only when their data changes by content (`React.memo`, content-stable derived rows). Check that other reports' tile toggles do not cause the same issue.
