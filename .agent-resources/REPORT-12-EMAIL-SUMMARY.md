# Report 12: Email summary

Twelfth report. Audience: email marketers. PLAN "Email summary across all emails": sends, open rate, click rate, bounces, unsubscribes, top and bottom performers. Time-range report in the **Emails** section, next to Recipient lists. Read `PLAN.md`, `REPORT-11-RECIPIENT-LISTS.md` (closest: same section, DB access, seeding rules), `REPORT-09-MEMBERS.md` (DB access) and `NAV-SECTIONS.md` first. Build on existing code; do not duplicate. Keep complexity low.

## Product model (decompiled 31.9, checked 2026-10-01)

Public Info classes: `EmailConfigurationInfo`, `EmailStatisticsInfo`, `EmailStatisticsHitsInfo`, `EmailLinkInfo`, `EmailChannelInfo` (`CMS.EmailLibrary`), `SendConfigurationInfo` (`CMS.EmailMarketing`). Reference them in SQL doc comments like earlier reports.

- **`EmailLibrary_EmailConfiguration`**: one row per email. `...Name` (code name), `...Purpose` nvarchar (`Regular`, `Automation`, `FormAutoresponder`, `Confirmation`, `CommerceOrderStatusChange`, maybe more; read the `EmailPurpose` values instead of hardcoding this list), `...EmailChannelID`, `...ContentItemID`, `...LastModified`.
- **Display name**: the native list joins `CMS_ContentItemLanguageMetadata` on the content item and shows `ContentItemLanguageMetadataDisplayName`. Use the email channel's primary language (`EmailChannelPrimaryContentLanguageID`), and fall back to any language, then to the code name.
- **`EmailLibrary_SendConfiguration`** (Regular emails only): `...Status` (`SendConfigurationStatus`: 0 Draft, 1 Scheduled, 2 Sending, 3 Sent), `...ScheduledTime` (the native **Send date** column), `...RecipientListID`.
- **`EmailLibrary_EmailStatistics`**: one row per email, **lifetime totals with no timestamps**: `TotalSent`, `EmailsDelivered`, `EmailOpens`, `EmailUniqueOpens`, `EmailClicks`, `EmailUniqueClicks`, `EmailSoftBounces` (NULL), `EmailHardBounces` (NULL), `UniqueUnsubscribes`, `SpamReports` (NULL). These are the native numbers.
- **`EmailLibrary_EmailStatisticsHits`**: raw events with time. `...Type` (`EmailStatisticsHitsType`: 0 Sent, 1 Open, 2 Click, 3 SoftBounce, 4 HardBounce, 5 Unsubscribe), `...Time` (server local time, `DateTime.Now`), `...MailoutGUID` (one per recipient send), `...EmailLinkID` (clicks), `...IsProcessed`. No product code deletes hits (checked in `CMS.EmailMarketing` only; check the cleanup tasks as well).
- **Recalculation** (`Proc_EmailLibrary_EmailStatistics_RecalculateEmailStatistics`, run by the scheduled task `EmailStatisticsCalculatorTask` and by the Statistics tab "Refresh" button). It works in two ways:
  - Totals (sent, opens, clicks, bounces) are **added up step by step** from unprocessed hits, which are then marked processed.
  - Uniques are **recalculated from all hits**, per mailout: an open = any open **or click** hit, a click = any click hit, an unsubscribe = any type 5 hit.
  - Delivered = sent − soft − hard bounces.
  - So hits can rebuild uniques exactly, but not always totals.
- **Bounces, spam and delivery providers**: the SMTP fallback calculator counts bounces from hits (written by `BounceChecker`). Other providers (`ISpecificEmailStatisticsCalculatorCreator`, for example SendGrid on the Community Portal in production, where spam reports show 0 and not "-") can write bounces, spam and delivered directly into the statistics table without hits. **Use the statistics table for bounces, spam and delivered. Never use hits for them.**
- **Rates differ inside the product**:
  - Email list (`EmailStatisticsFormatters.FormatStatisticsRate`): unique ÷ **TotalSent**.
  - Statistics tab: open/click rate = unique ÷ **Delivered**. Delivery rate = delivered ÷ sent. Hard/soft bounces, spam and unsubscribe rate are each ÷ sent.
  - Example from production, Newsletter #41: list 55.1% (75/136), tab 55.6% (75/135).
  - Follow the Statistics tab (Decisions 1) and say in a hint that the email list uses sent.
- **Native links** (user-confirmed URLs, 2026-10-01): list `/admin/emails-1/en/list` (`EmailList`), email `/admin/emails-1/en/list/3/content` (`EmailContentTab`). Statistics = `EmailStatisticsTab`, slug `statistics` under `EmailEditLayout`.
  - Parameters: `EmailChannelApplication` = `"emails-" + EmailChannelID` (the prefix `emails` is the internal `EmailPagePathSlugs.APPLICATION`, so keep it as a constant with a comment), `EmailChannelContentLanguage` = language code name, `EmailEditLayout` = EmailConfigurationID.
  - All page types are `public sealed`; the product helper (`EmailChannelLinkParametersHelper`) is internal, so build `PageParameterValues` in `IStatsAdminLinks`.
  - Verify the generated path matches the confirmed URLs.

## Local data

DB access as in `REPORT-09-MEMBERS.md`.

**Community Portal DB** (`Kentico.Community`, same server and credentials; read only, never write). Use it to check SQL against real data:
- 45 emails, 1 email channel. 31 statistics rows, 343 hits (Sep 2025 – Aug 2026), 189 links.
- Newsletters (Regular, Sent) have zero statistics, no hits and a NULL ScheduledTime, because this local copy never sent them. Only 5 automation/autoresponder emails have hits (e.g. 39 "Member registration email confirmation": sent 68, unique opens 94, unique clicks 83).
- Uniques from hits match the statistics table (open = open or click per mailout). Use this as a SQL test.
- 44 mailouts have open/click hits but no sent hit, and unique opens can be higher than sent. **Rates can go over 100%.** Show the number and do not cap it; add a hint.
- Open lag after a send: average 99 min, max 31 h.

**Production Community Portal** (screenshots shared by the user): ~41 biweekly newsletters, sent 126–140 each, open rate 45–57%, click rate 9–20%, hard bounces 0–1, unsubscribes 0–1, spam 0. Automation "Q&A discussion notification": 172 sent, 96.5% opens, 61.6% clicks. Model the seed on these numbers.

**DancingGoat DB**: 7 emails, 1 email channel (ID 1, primary language ID 1, 2 languages). Regular emails 2 and 3 are Draft. 1 statistics row and 2 hits are real. **Never delete or change real rows.**

### Seeding (local dev DB only, `.agent-resources/seed-email-statistics.sql`, re-runnable)

The report needs ~12–20 sent Regular emails. Emails are channel content items (all the `CMS_Content*` tables), so **the seed does not create emails** (Decisions 3).

- **Reference send (user, first)**: send one Regular email to the DancingGoat recipient list through Mailpit, then open it and click links as several contacts. Before writing the seed, look at every row it created or changed:
  - `SendConfiguration`
  - `EmailMarketingRecipient`
  - hits: types, times, MailoutGUIDs, link IDs
  - `EmailLink`
  - `EmailStatistics` before and after the recalculation
  - email activities in `OM_Activity`
  The seed must write the same shape (columns, values, relations) at a larger scale. Never change these real rows.
- **What the real send showed (2026-10-01)**: the user cloned 13 newsletters (IDs 8–20; #12 = ID 20 is unsent; IDs 17 and 18 are both named "#10") and sent them to the DancingGoat list. They also clicked links in "Dancing Goat Regular" (ID 2) as several contacts.
  - A send sets `SendConfiguration` Status 3 + ScheduledTime = the send time. It also sets the content dates: metadata Created/Modified, CommonData First/LastPublished, and `EmailConfigurationLastModified`.
  - Each email got 145 sent hits (one per receiving member), unprocessed until recalculation. `EmailMarketingRecipient` rows are deleted after the send. There are 3 `EmailLink` rows per clone.
  - Clicks store `EmailLinkID`; opens have none.
  - `emailclick` activities: `ActivityItemID` = EmailConfigurationID, `ActivityItemDetailID` = EmailLinkID, `ActivityValue` = link URL, `ActivityChannelID` = email channel, title "Clicked link in email '<name>'".
  - There are no open activities.
- **Seed as built** (`.agent-resources/seed-email-statistics.sql`, run 2026-10-01; this replaces the clone/link steps below):
  - It moves the send and content dates of the sent `(seed)` emails to every 14 days, the newest 7 days ago (Apr 23 – Sep 24). It shifts the real sent hits by the same amount.
  - On the real mailout GUIDs it generates opens (45–57%; lag 50% < 3 h, up to 7 days; some repeat opens), clicks (9–20%; 8% with no open hit) and 0–2 unsubscribes, plus 0–1 hard bounces as hits.
  - It deletes the seed emails' statistics rows and recalculates them with the product proc.
  - Result: open rate 48.6–59.3%, click rate 8.3–18.6%. Re-runs give identical results.
  - It does not seed `emailclick` activities (optional later).
  - Update 2026-10-01: real opens/clicks (user, on #10 ID 17 and #12 ID 20) have the same MailoutGUID as the email's sent hits, and one `emailclick` activity per click hit. Malformed links (`https://localhost/articles` in clones 8–19) are still tracked. Seeded hits are now marked by a time fraction of exactly 700 ns. Re-runs delete only marked hits, move real hits and `emailclick` activities with the send date, and skip mailouts that have real engagement. #12 (ID 20) is included (newest, Sep 25). #13 (ID 21) is unsent.
- **Manual step (user)**: clone "Dancing Goat Regular" ~12–16 times with the native Clone action. Name the clones `Newsletter #N (seed)` and publish them, but do not send them.
- The seed picks emails whose primary-language display name ends with ` (seed)`. With none, it prints a message and stops. Sort by N: newsletter n gets the send date now − 14 × (count − n) days.
- It updates the clone's existing `SendConfiguration` row: Status 3, ScheduledTime = send date, RecipientListID = the DancingGoat list. This is the only change to product-created rows, and only for `(seed)` emails. Check first how the row looks after the clone + publish. If there is no row, insert one with a fixed GUID.
- If the clone has no `EmailLink` rows, it inserts 2–3 with fixed GUIDs.
- Done when the native email list shows them as Sent with a send date, and the Statistics tab opens with the seeded numbers. If status 3 without a real send breaks the native UI, stop and report.
- Use fixed GUIDs (MD5 of `'SEED-EM|configGUID|step'`) for everything the seed inserts.
- Hits per newsletter, from contacts with an email, picked the same way as in `seed-recipient-lists.sql` (CHECKSUM):
  - sent: ~120–140 mailouts at the send time
  - opens: 45–57% of mailouts, 0–48 h later, some with 2–3 opens
  - clicks: 9–20% of mailouts, on one of the email's links (seed `EmailLink` rows)
  - unsubscribes: 0–2
  - hard bounces: 0–1
  - Keep a few clicks without an open hit, so the "click counts as open" rule is tested.
- Automation emails: no seed. The user's real Mailpit sends (Decisions 4) cover them.
- Statistics rows: compute them in the seed from the seeded hits, with the same rules as the proc. Insert the hits as processed.
  - Then run `EXEC Proc_EmailLibrary_EmailStatistics_RecalculateEmailStatistics @EmailConfigurationID = n` for one email, as a check that the numbers stay the same.
- Drop events after now. On re-run, delete the seeded hits, statistics rows and links of `(seed)` emails (hits and stats rows have no GUID; the target emails are seed-only), then insert them again. Never delete content items. Never put seed data in `src/`.

## Scope

### Server

1. **Page**: `EmailSummaryPage`, slug `email-summary`, name "Email summary", order before Recipient lists, icon `Icons.Graph` (check the enum member), template `@kentico/xperience-admin-labs-simple-stats/EmailSummary`, derived from `StatsReportPage<>`.
   - Permission `StatsPermissions.EMAIL_SUMMARY` = `SimpleStats.EmailSummary`, "Email summary".
   - `StatsNavigation` tests: only-this-permission lands on it; with both email permissions, Email summary comes first.
2. **Filter**: `StatsFilter` (range + grouping) plus an optional **email channel** filter, through the existing channel filter with email channels only. With one email channel, hide it. Do not change other reports' cache keys.
3. **Definitions** (UI hints + usage guide):
   - *Sent in range* (Regular emails): `SendConfigurationStatus` Sent or Sending, and `ScheduledTime` in the range. This sets the scope for KPIs, the email table and top/bottom.
   - *Email numbers* are the lifetime totals from the statistics table, the same as the native Statistics tab. Opens and clicks that come after the range still count toward an email sent in the range; say so.
   - *Open rate / click rate* = unique ÷ delivered. *Delivery rate* = delivered ÷ sent. *Bounce, unsubscribe and spam rate* = ÷ sent. When the denominator is 0, the rate is `null`. Aggregate rates use **sums** (Σ unique opens ÷ Σ delivered), not the average of the per-email rates. Change is shown in pp.
   - Soft bounces, hard bounces and spam can be NULL (not tracked by the provider). Show "-" and leave them out of the sums. If every row is NULL, hide that column or KPI.
   - *Activity over time*: from hits, all purposes. Count of sent hits per bucket; unique opens = distinct mailouts with an open or click hit in the bucket; same for clicks and unsubscribes. The buckets are when people acted, not cohorts by send date.
4. **Queries** (one round trip, parameterized, aggregated in SQL, current + previous period; if tables are missing, return an empty result + a hint):
   - Regular emails sent in the range and in the previous period: id, name, channel, language code, send date, recipient list, and the statistics columns.
   - Automated emails (every purpose except Regular): lifetime statistics + sent hits in the range. Show only emails with any statistics.
   - Daily activity from hits for the previous period + the range (channel filter through `EmailConfigurationEmailChannelID`).
   - Optional, only if cheap: top links by unique clicks in the range (`EmailLink` target + description). Skip it if it adds noise.
5. **Top / bottom performers**:
   - Rank Regular emails sent in the range by open rate and by click rate. Show the top 5 and bottom 5, using the existing ranked snapshot.
   - Leave out emails with fewer than N delivered (constant, e.g. 10), and say so in a hint.
6. **Result** (suggested): `EmailSummaryResult { From, To, Grouping, ChannelId, Periods, Totals { Emails, Sent, Delivered, DeliveryRate, OpenRate, ClickRate, HardBounces, SoftBounces, Unsubscribes, UnsubscribeRate, SpamReports } (each a StatsValueComparison), Activity (sent, opens, clicks, unsubscribes series), Emails (rows + StatisticsUrl), Automated (rows), TopByOpenRate, BottomByOpenRate, TopByClickRate, BottomByClickRate, ChannelOptions, EmailsAppUrl, Available, UpdatedAt }`.
7. **Service**: interface + `StatsCache`. Tests: builder, service, SQL. Cover:
   - no emails, no statistics, missing tables
   - NULL bounce/spam columns
   - rate null when delivered is 0
   - rate over 100%
   - sum vs average aggregation
   - Draft/Scheduled emails excluded
   - send date on the range edges
   - a click without an open hit counts as a unique open
   - the minimum-delivered threshold for top/bottom
   - channel filter; unknown channel → All
   - link parameters

### Client

- Template `email-summary/EmailSummaryTemplate.tsx`, exported in `entry.tsx` (with the export-permission wrapper).
- Filter bar: range + grouping (+ channel when there is more than one), refresh, updated at.
- KPI row: Emails sent (count of emails), Sent, Open rate, Click rate. Second row: Delivery rate, Hard bounces, Unsubscribe rate, Spam reports (hide when NULL). Reuse `ComparisonInfoCard`.
- Tile "Email activity over time": `ComboChart` lines (sent, unique opens, unique clicks) or `StackedColumnChart`, table toggle, CSV.
- Tile "Emails sent in range" (full width): `RankedTable` with name (link to Statistics), send date, sent, delivered, open rate, click rate, hard bounces, unsubscribes. Chart toggle: `RankedBarChart` of open rate. CSV.
- Tile "Top and bottom performers": open rate / click rate switch, top 5 + bottom 5.
- Tile "Automated emails": lifetime table (purpose, sent in range, lifetime sent, open rate, click rate), with a link to Statistics. Hint: these are lifetime numbers, not limited to the range.
- Link "Open emails" (the native email list for the channel).
- Hints / `DataRetentionNote`: the definitions; rates follow the Statistics tab (the email list uses sent); bounces and spam depend on the delivery provider; deleting an email deletes its statistics.
- Empty state when no Regular email was sent in the range.

## Look and feel, goal, done when

Same as `REPORT-08-CUSTOMERS.md`, plus:

- For 3 seeded newsletters and 1 automation email, the numbers match the native Statistics tab.
- The links open the right Statistics tab. Check the generated path against `/admin/emails-1/en/list/{id}/statistics`.
- Run the SQL read-only against `Kentico.Community` once. The uniques must match its statistics table for the 5 emails with hits.
- `docs/Usage-Guide.md`: report section, permission row, navigation table.
- A real local send (Mailpit) shows up in the report after recalculation, and its numbers match the Statistics tab.
- Follow `dotnet format`: an uncovered switch case throws `ArgumentOutOfRangeException` or returns a handled error state, never a silent default.
- Do not commit.

## Decisions (user, 2026-10-01)

1. **Rates**: open/click rate = unique ÷ delivered, as in the Statistics tab.
2. **Scope**: KPIs, the email table and top/bottom cover **Regular emails only**. Automated emails (Automation, FormAutoresponder, the others) go in a separate lifetime table. Reason: Regular emails are always marketing, sent to a recipient list with double opt-in. The other purposes mix transactional and marketing sends (legitimate interest, commerce, password recovery). This reasoning is for the code docs only; **do not put it in the report UI**.
3. **Seeding**: the user clones emails in the UI (native Clone; SQL cloning of `CMS_Content*` rows is too fragile). The seed adds only the send state, links, hits and statistics.
4. **Real send**: done. `examples/DancingGoat/Program.cs` adds `AddXperienceChannelSmtp("DancingGoatEmails", ...)` with the `SystemSmtpOptions` settings (Mailpit on `localhost:1025`), and `docs/Contributing-Setup.md` mentions it. The user will test real sends locally with several contacts. Use that as the check of hit logging + recalculation (run the scheduled task or use the Statistics tab "Refresh"), next to the seeded data. Real sends create real rows that the seed must not touch.
5. **Production check** (user, after the build): the user will install a prerelease package on the Community Portal production site (SendGrid sends) and compare the report with the native Statistics tab and the email list. So:
   - the report must work when bounces, spam and delivered come from SendGrid and not from hits
   - it must work with ~45 emails and one email channel
   - the page must not break when hits are missing
   - list in the report-back what the user should compare there (per newsletter: sent, delivered, open/click rate, hard bounces, unsubscribes, spam; range totals vs the sum of the native rows)

Report back concise: files, SQL shape, definitions chosen, shared changes, links, seeding, checks, not checked, open questions.
