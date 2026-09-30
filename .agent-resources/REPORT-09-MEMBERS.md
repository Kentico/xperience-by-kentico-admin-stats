# Report 09: Member registrations

Ninth report. Audience: marketers, site owners with member areas. PLAN "Later candidates → Other → Member registrations over time". Time-range report. Read `PLAN.md`, `REPORT-08-CUSTOMERS.md` (closest: growth combo chart, cumulative totals, ratio KPIs) and `NAV-SECTIONS.md` first; skim 01–07 as needed. Build on existing code; do not duplicate. Keep complexity low.

## Local data (checked 2026-09-30)

DB: `mssql2022` docker, DB `xperience-by-kentico-admin-stats`, creds in `examples/DancingGoat/appsettings.json`. Run SQL from Git Bash: `MSYS_NO_PATHCONV=1 docker exec -i mssql2022 /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P '<password>' -d xperience-by-kentico-admin-stats -W < file.sql`. Package 31.9.0.

- `CMS_Member` (147 rows, seeded by `.agent-resources/seed-members.sql`, re-runnable): `MemberID`, `MemberEmail` (unique, NULL), `MemberName` (unique, NULL), `MemberEnabled` bit, `MemberCreated` datetime2 NOT NULL, `MemberGuid`, `MemberIsExternal` bit, `MemberPassword`, `MemberSecurityStamp`. Created Jul 5 – Sep 29; 12 disabled, 17 external.
- `CMS_MemberRole` / `CMS_MemberRoleMember`: 0 rows (see Seeding).
- `OM_Activity` type `memberregistration` (171 rows, 98 seeded): contact-level registration events. Not the source of truth for member counts (activities can be deleted by cleanup / contact deletion); members table is.
- Every seeded member is a contact with the same email (user decision: membership sites need tracking). **Profiles** (`OM_Profile`, `OM_ProfileReference`) are a preview feature, off in DancingGoat → do not use them.
- Verify public Info classes (`MemberInfo`, `MemberRoleInfo`, `MemberRoleMemberInfo`, probably `CMS.Membership`) and reference them in SQL doc comments like earlier reports.
- Native app: Members, `Kentico.Xperience.Application.Members`, slug `members`, icon `xp-user-frame` (decompiled). Link via `IStatsAdminLinks`; per-member link only if the member edit page type/route is public and simple (verify), else skip + say why.

### Seeding (local dev DB only, `.agent-resources/`)

Extend `seed-members.sql` (keep it re-runnable) or add `seed-member-roles.sql` run after it: 3–4 member roles (e.g. "Premium", "Partners", "Newsletter only" — code names prefixed `Seed`), ~60% of seeded members in 1–2 roles, uneven sizes. Check NOT NULL columns + FKs first. Never in `src/`.

## Scope

### Server

1. **Page** `MembersPage`, slug `members`, name "Member registrations", icon `Icons.UserFrame` (or the enum member matching `xp-user-frame`, verify), template `@kentico/xperience-admin-stats/Members`. Parent: **Contacts** section (`StatsContactsSection`), order after existing Contacts reports. Derive from `StatsReportPage<>` (Export flag). Permission `StatsPermissions.MEMBERS` = `Kentico.Xperience.AdminStats.Members`, "Member registrations", same pattern (`UIPermission` on app, `UIEvaluatePermission` on page, `PageCommand.Permission`). Nav filtering in `StatsNavigation` must pick it up without special cases (add to its tests).
2. **Filter**: shared `StatsFilter` range + grouping; channel normalized to null (members are global). No extra filters. Do not change other reports' cache keys.
3. **Definitions** (UI hints + usage guide):
   - *New member*: `MemberCreated` in range.
   - *Total members*: members created on or before a date that still exist (deleted members disappear; say so).
   - *External*: `MemberIsExternal` (signed up through an external sign-in provider).
   - *Disabled*: `MemberEnabled = 0` now (current state, not historical).
4. **Queries** (one round trip, parameterized, aggregate in SQL, current + previous period via `StatsComparison.GetPreviousRange`; missing table → empty result + hint, reuse the availability-check pattern):
   - Daily new members, previous period + range, split internal / external; count of members created before the range start (cumulative start).
   - Totals current vs previous: new members, total members at period end, external share of new members (ratio, `null` when 0 new), disabled members (current snapshot, no comparison or comparison omitted — say which).
   - Members by role (current snapshot, TOP N, plus "No role" row), with new-in-range count per role.
5. **Shared infra**: reuse `BuildCumulativeSeries`, `StatsValueComparison` (Count / Ratio with pp change), ranked, combo. Add nothing new unless needed; if you do, make it shared.
6. **Result** (suggested): `MembersResult { From, To, Grouping, Periods, NewMembers (series, internal + external), TotalMembers (cumulative), Totals { NewMembers, TotalMembers, ExternalShare, DisabledMembers }, ByRole, MembersAppUrl, Available, UpdatedAt }`.
7. **Service**: interface + `StatsCache` (5 min, refresh drops key). Tests: builder, service, SQL (0 members, missing table, all external, previous 0 → change null, cumulative start, members without roles, ratio null).

### Client

- Template `members/MembersTemplate.tsx`, export in `entry.tsx` (wrapped with the export permission like the others).
- Filter bar: range + grouping, refresh, updated at.
- KPI row (4): New members, Total members, External sign-ups (%), Disabled members.
- Tile "Member growth": `ComboChart` (stacked or plain columns = new members; if stacking internal/external in the combo is not already supported, use plain columns and put the split in the table — don't grow `ComboChart` for this) + line = total members; `TimeSeriesTable` toggle; CSV.
- Tile "New members by sign-in type": `DonutChart` / `ShareTable` (internal vs external in range), CSV.
- Tile "Members by role": `RankedBarChart` / `RankedTable` (current members per role, new in range as secondary value), CSV. Hint: roles are current state.
- Link "Open members" to native Members app.
- `DataRetentionNote` / hints: definitions above; deleted members disappear; disabled is current state.
- Empty state when there are no members.

## Look and feel, goal, done when

Same as `REPORT-08-CUSTOMERS.md` ("Look and feel — must feel native", "Goal", "Done when"), with: nav shows the report under Contacts; a role with only this permission lands on it; Export flag hides CSV. `docs/Usage-Guide.md`: report section, permission row, navigation table. Follow `dotnet format`; an inserted switch throw means an uncovered case → keep a throw (`ArgumentOutOfRangeException`) or a handled error state, never a silent default. Do not commit. Report back concise: files, SQL shape, definitions, shared changes, links (or why skipped), seeding, checks, not checked, open questions.
