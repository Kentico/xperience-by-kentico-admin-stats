# Xperience by Kentico Labs: Admin Stats

Planning notes for a Kentico Labs extension that shows basic charts and tables about Xperience by Kentico data in the administration UI.

## Naming

- **Title:** Xperience by Kentico Labs: Admin Stats
- **Repo:** `xperience-by-kentico-admin-stats`
- **NuGet package:** `Kentico.Xperience.AdminStats` (follows `Kentico.Xperience.ContentModelGraph`)
- **Admin application name:** Stats (Labs)

## Scope

This project is a Labs example for basic admin charts. It is not Kentico's plan for reporting in Xperience by Kentico. Native reporting, reporting through AIRA, or data exposed to external agents may come later in the product.

Suggested first line of the README:

> This project is a Kentico Labs example that shows basic charts about your Xperience by Kentico data in the administration. It is not Kentico's plan for reporting in Xperience by Kentico.

Design goals:

- Keep the reports mostly static, with a small set of shared filters.
- Use data that XbK already stores with timestamps.
- No extension points for other libraries or custom reports.

## Reports and data

### Recommended for the first release

| Report                              | Data source                         | Notes                                                                          |
| ----------------------------------- | ----------------------------------- | ------------------------------------------------------------------------------ |
| Activity counts by type over time   | Contact activities                  | Page visits, form submissions, email opens and clicks, custom activities       |
| Top pages by visits                 | Contact activities (URL field)      | For a selected date range                                                      |
| Top referrers                       | Contact activities (referrer field) | Only where a referrer was recorded                                             |
| New contacts over time              | Contacts (created date)             | Include the ratio of identified contacts (with email) to anonymous contacts    |
| Form submissions per form over time | Form data tables                    | One query per form; ranked list of most and least used forms                   |
| Email summary across all emails     | Email statistics                    | Sends, open rate, click rate, bounces, unsubscribes; top and bottom performers |

### Later candidates

**Contacts and contact groups**

- Contact group sizes over time. Membership is current state only, so this needs a scheduled task that saves daily counts to a custom table.

**Emails**

- Recipient list growth and unsubscribes over time. Spec: `.agent-resources/REPORT-11-RECIPIENT-LISTS.md`.

**Content** (useful for administrators and content leads)

- Content items by content type, by workflow step, and by language.
- Items in a workflow step for a long time ("action needed").
- Content not modified in 6 or 12 months.
- Items missing translations for a given language.
- Reusable items with no usages. _Uncertain: not yet checked whether usage tracking data is easy to query._

**Consents**

- Agreements and revocations per consent over time.

**Other**

- Digital commerce orders and revenue. _Uncertain: not yet checked which commerce tables are stable enough to depend on._
- Customer growth over time, broken down by billing or shipping country and state; rank top customers by revenue, order count, and item quantity purchased.
- Member registrations over time.
- Admin user sign-in activity.

**Event log** (useful for administrators and developers)

Data source: `CMS_EventLog` (`EventType`, `EventTime`, `Source`, `EventCode`, `UserID`, `UserName`, `EventDescription`, `EventUrl`, `EventMachineName`).

- Event types: Information (`I`), Warning (`W`), Error (`E`).
- Events can come from any admin UI user (`UserID` / `UserName`) or from the system (no user).
- `Source` and `EventCode` have a small, fairly fixed set of values, so they group well.

Report ideas:

- Events by type over time (stacked, by day, week, or month), with a filter for event type.
- Totals per type compared with the previous period (reuse `StatsComparison`), for example "Errors +40% vs previous 30 days".
- Top sources and top event codes (ranked), each with a change vs the previous period.
- Top users by event count, with system events shown as their own row.
- Optional: link to the native Event log application (via `IStatsAdminLinks`).

Notes:

- The event log is trimmed by a size limit setting (verify the setting name), so old events disappear. Add a data retention note.
- The table can be large on busy sites. Aggregate in SQL and use `TOP N`.
- Local DancingGoat DB (2026-09-29): 582 events (532 I, 44 W, 6 E), 28 sources, 24 codes, 2 users, all from one day. Trends over time need seeded data.

### Data limitations

- Contact and activity cleanup tasks delete old data. Long-range trends may show drops that are really deletions. Add a note on the dashboard about data retention.
- The activity table can be large. Aggregate in SQL and cache results, or precompute with a scheduled task.
- Data that is current state only (for example contact group membership) needs periodic snapshots to show trends.

## Filter options

| Filter     | Details                                                                              |
| ---------- | ------------------------------------------------------------------------------------ |
| Date range | Presets for 7, 30, and 90 days, plus a custom range. Applies to the whole dashboard. |
| Channel    | Website channel or email channel, where the data has a channel ID.                   |
| Grouping   | Day, week, or month for trend charts.                                                |

## Library features

| Feature                       | Details                                                                                              |
| ----------------------------- | ---------------------------------------------------------------------------------------------------- |
| Chart and table toggle        | Each tile can switch between the chart and a table of the exact numbers.                             |
| CSV export                    | Per report. Guarded by one app-wide Export permission (see Implementation notes).                    |
| Links to existing admin pages | For example, a form links to its submissions and an email links to its statistics.                   |
| Permission per report         | Uses the standard XbK role and UI permission model, so marketers and admins can see different tiles. |
| Show/hide tiles per user      | Stored in a small custom table. Moderate effort; optional for the first release.                     |

## Implementation notes

- Build as a custom admin application with a custom React page template.
- Use amCharts for charts. It ships with Xperience by Kentico and is available to admin React components, so there is no extra bundle size or licensing question.
- Load data through page commands.
- Reference: the Community Portal reporting admin UI (`CommunityStatsLayoutTemplate.tsx` in the `Kentico/community-portal` repo), linked from the Admin Design Components README.
- Stats should have their own application permissions to help administrators limit who has access to the information
- **Permission per report page.** Today `StatsApplicationPage` declares only `SystemPermissions.VIEW`, and every report page checks VIEW. Change to one custom permission per report page:
  - Declare each permission on the application page with `[UIPermission("<name>", "<display name>")]`, for example `Kentico.Xperience.AdminStats.ActivityCounts` / "Activity counts". These show up in **Role management** for the Stats (Labs) application.
  - Keep `[UIPermission(SystemPermissions.VIEW)]` for access to the application itself.
  - Restrict each report page with `[UIEvaluatePermission("<name>")]`. It must be one of the permissions declared on the application, or it cannot be assigned to roles.
  - Set the same permission on each page's `LOAD` command (`[PageCommand(Permission = "<name>")]`) so the data can't be read without it.
  - Keep permission names as constants in one class (e.g. `StatsPermissions`), with a stable `Kentico.Xperience.AdminStats.` prefix.
  - Verify: roles without a report permission get 403 on that page. Reports are grouped in section pages (Contacts, Content, Commerce, System; `.agent-resources/NAV-SECTIONS.md`). Decompiled 31.9 code shows the product does not filter nav or default routes by permission, so `StatsNavigation` hides denied reports and empty sections and lands on the first allowed report, or on the hidden "No reports available" page. Verified in DancingGoat 2026-09-30 with test users: View only; View + New contacts; View + New contacts + Customers. Opening a denied report by URL shows the product's "Access Denied" page. Export permission hides "Export CSV" in all reports (verified same day).
  - Update `docs/Usage-Guide.md` with the permission list and how to assign them in Role management.
  - **Export permission.** One app-wide permission guards "Export CSV" in every report, separate from the report permissions:
    - `StatsPermissions.EXPORT` = `Kentico.Xperience.AdminStats.Export`, "Export", declared on `StatsApplicationPage` with `[UIPermission]`.
    - Each report page checks it server side (`Page.UIPermissionEvaluator` / `IUIPermissionEvaluator.Evaluate(StatsPermissions.EXPORT)`) and sends a `CanExport` flag in its client properties. Put this in one shared place (base page class or helper), not per report.
    - Client: `StatsTile` hides "Export CSV" when `CanExport` is false (shared, e.g. context or prop from each template). No disabled button; just hidden.
    - Caveat: CSV is built client side from data the user can already see, so this is a UI guard, not data protection. Say so in `docs/Usage-Guide.md`. Server-side export (a page command returning CSV with `Permission = EXPORT`) only if we later need real protection.
    - **Export events** (best effort, user decision 2026-10-01): every export raises `AfterExportStatsEvent` through a shared `LOG_EXPORT` command, for audit handlers. The product `AfterExportListingEvent` cannot be raised by libraries. Spec: `.agent-resources/EXPORT-EVENTS.md`.
    - Tests: flag true/false per page; permission declared on the app. Usage guide permission table gets the row.
  - Reference: [UI page permission checks](https://docs.kentico.com/documentation/developers-and-admins/customization/extend-the-administration-interface/ui-pages/ui-page-permission-checks) (define with `UIPermission` on the `ApplicationPage`, evaluate with `UIEvaluatePermission`, `PageCommand.Permission`, `IUIPermissionEvaluator` for client-side flags).

## Out of scope

- Extension points for other integrations or custom reports.
- Ad hoc report builder or query designer.
- Arbitrary SQL input (also a security risk).
- Free drag-and-drop layouts.
- Scheduled report emails.
- Real-time updates.
