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

- Recipient list growth and unsubscribes over time.

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
- Member registrations over time.
- Event log errors over time.
- Admin user sign-in activity.

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
| CSV export                    | Per report.                                                                                          |
| Links to existing admin pages | For example, a form links to its submissions and an email links to its statistics.                   |
| Permission per report         | Uses the standard XbK role and UI permission model, so marketers and admins can see different tiles. |
| Show/hide tiles per user      | Stored in a small custom table. Moderate effort; optional for the first release.                     |

## Implementation notes

- Build as a custom admin application with a custom React page template.
- Use amCharts for charts. It ships with Xperience by Kentico and is available to admin React components, so there is no extra bundle size or licensing question.
- Load data through page commands.
- Reference: the Community Portal reporting admin UI (`CommunityStatsLayoutTemplate.tsx` in the `Kentico/community-portal` repo), linked from the Admin Design Components README.
- Stats should have their own application permissions to help administrators limit who has access to the information

## Out of scope

- Extension points for other integrations or custom reports.
- Ad hoc report builder or query designer.
- Arbitrary SQL input (also a security risk).
- Free drag-and-drop layouts.
- Scheduled report emails.
- Real-time updates.
