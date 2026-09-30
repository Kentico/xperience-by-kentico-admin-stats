# Usage Guide

## Setup

1. Install the `Kentico.Xperience.AdminStats` NuGet package in your Xperience by Kentico web project.
2. Run the application. No service registration is needed; the library registers its services and admin UI automatically.

## Application

The library adds the **Stats (Labs)** application to the **Digital marketing** category.

## Navigation

Reports are grouped into sections. Opening the application or a section opens its first report the role may see.

| Section  | Reports                                                                                    |
| -------- | ------------------------------------------------------------------------------------------ |
| Contacts | Activity counts, Top pages, New contacts, Form submissions, Member registrations, Consents |
| Content  | Content inventory                                                                          |
| Commerce | Orders and revenue, Customers                                                              |
| System   | Event log                                                                                  |

Sections have no permission of their own. A section is hidden when the role has no permission for any of its reports. A role with **View** but no report permission sees a **No reports available** message.

## Permissions

- Administrators see the application and all reports by default.
- For other roles, open **Role management**, select the role, and edit its permissions for **Stats (Labs)**:
  - **View** - opens the application.
  - One permission per report. The role sees only the reports it has a permission for. Other reports are hidden from the navigation and return an error if opened by URL.
  - **Export** - shows the **Export CSV** buttons in all reports the role can see. Without it, the buttons are hidden.

| Permission           | Code name                                       |
| -------------------- | ----------------------------------------------- |
| Activity counts      | `Kentico.Xperience.AdminStats.ActivityCounts`   |
| Top pages            | `Kentico.Xperience.AdminStats.TopPages`         |
| New contacts         | `Kentico.Xperience.AdminStats.NewContacts`      |
| Form submissions     | `Kentico.Xperience.AdminStats.FormSubmissions`  |
| Content inventory    | `Kentico.Xperience.AdminStats.ContentInventory` |
| Event log            | `Kentico.Xperience.AdminStats.EventLog`         |
| Orders and revenue   | `Kentico.Xperience.AdminStats.OrdersRevenue`    |
| Customers            | `Kentico.Xperience.AdminStats.Customers`        |
| Member registrations | `Kentico.Xperience.AdminStats.Members`          |
| Consents             | `Kentico.Xperience.AdminStats.Consents`         |
| Export               | `Kentico.Xperience.AdminStats.Export`           |

The **Export** permission only hides the buttons. The CSV is built in the browser from the data the report already shows, so a role that can see a report can still copy its numbers. It is not data protection.

## Reports

### Activity counts

Shows contact activities per activity type over time as a stacked column chart.

- **KPIs** - total activities, top activity type, and number of activity types in the range.
- **Filters**
  - **Date range** - last 7, 30 or 90 days, or a custom range (up to about 5 years).
  - **Group by** - day, week (weeks start on Monday) or month.
  - **Channel** - all channels, or one website or email channel (uses the activity channel).
- **Chart / table** - switch the tile to a table with the exact numbers.
- **Export CSV** - downloads the table as a CSV file.

### Top pages

Shows the 25 most visited page URLs in the range as a bar chart, largest on top.

- **KPIs** - total page visits, number of distinct page URLs, and the top page.
- **Filters** - date range and website channel (no grouping).
- **Chart / table** - the table lists rank, URL (opens the page in a new tab), visits, unique contacts and share of all page visits in the range.
- **Export CSV** - downloads the list as a CSV file.

Visits are grouped by the logged URL with the query string and fragment removed, so `/page?utm_source=x` counts as `/page`. Other differences (host, trailing slash, letter case) count as different pages.

### New contacts

Shows contacts created per period, stacked by identified and anonymous, and the share of each.

- **Identified** - the contact has an email address. **Anonymous** - no email address (for example, a site visitor who has not submitted a form).
- **KPIs** - new contacts in the range, identified (count and share) and anonymous (count and share).
- **Filters** - date range and grouping (no channel; contacts have no channel).
- **Tiles** - "New contacts over time" (stacked column chart or table) and "Identified vs anonymous" (donut chart or table). Each has its own CSV export.

Counts only include contacts that still exist. Contacts deleted by cleanup and contacts removed when merged into another contact are not counted. A contact that gets an email address later counts as identified in the period it was created.

### Form submissions

Shows submissions per form over time and ranks all forms from most to least used.

- **Source** - counts come from the form data tables (`FormInserted`), so they include every stored submission, not only submissions logged as contact activities.
- **KPIs** - total submissions (with the change vs the previous period of the same length, for example "+12% vs previous 30 days"), average submissions per day, and forms with no submissions.
- **Filters** - date range and grouping (no channel; form data has no channel).
- **Tiles** - "Submissions over time" (stacked column chart per form or table; the top 5 forms are shown, the rest are grouped as Other) and "Forms by submissions" (bar chart or table of every form, including forms with 0). Click a form's graph bar or name in the table to open the form's **Submissions** tab in the **Forms** application. Each tile has its own CSV export.

Deleting a contact (manually or by inactive contact cleanup) deletes their activities but not their form submissions, so submissions stay counted. Submissions are removed only when editors delete them or through [personal data erasure](https://docs.kentico.com/x/04B1CQ).

### Member registrations

Shows [members](https://docs.kentico.com/documentation/business-users/members) (visitors with a site account) registered over time, total members, external sign-ups and members per member role.

- **Definitions**
  - **New member** - a member account created in the range (`MemberCreated`).
  - **Total members** (on a day) - members created on or before that day that still exist. Deleted members are not counted, also not for past days.
  - **External** - the member signed up through an external sign-in provider (for example Google or Microsoft). **Internal** - all other members.
  - **Disabled** - the member account is disabled now (current state, not historical).
- **KPIs** - new members, total members (on the last day of the range vs the last day of the previous period), external sign-ups (share of new members; the change is in percentage points, "pp"; "–" without new members), each vs the previous period of the same length, and disabled members (current state, no comparison).
- **Filters** - date range and grouping (no channel; members are global).
- **Tiles**
  - "Member growth" - new members as columns (left axis) and total members at the end of each period as a line (right axis), or a table with new, internal, external and total members.
  - "New members by sign-in type" - internal vs external new members in the range, as a donut chart or table.
  - "Members by role" - current members per member role, plus **No role** for members without a role, with how many of them were created in the range. Roles are the current state. A member can have several roles, so the share is of all members and shares do not add up to 100%. Roles without members are left out. Click a role to open it in the native **Members** application.
- **Open members** - opens the native **Members** application.
- Each tile has its own CSV export.

Deleted members are not counted. When the member tables do not exist, the report is empty.

### Consents

Shows [consent](https://docs.kentico.com/documentation/developers-and-admins/data-protection/consent-management) agreements and revocations over time, agreed contacts, all consents compared and which consent text agreed contacts agreed to.

- **Definitions**
  - **Agreement** / **revocation** - one agree or revoke action of a contact in the range. Agreeing again (for example to a new consent text) counts again.
  - **Agreed contacts** (on a day) - contacts whose latest agreement or revocation of the consent on or before that day is an agreement (the same rule the product uses). With **All consents**, each contact who agrees to at least one consent is counted once (not the sum per consent), so the number means "contacts you may process data of".
  - **Revocation rate** - revocations divided by agreements in the range ("–" without agreements; the change is in percentage points, "pp").
  - **Older text** - agreed contacts whose latest agreement was given to an older consent text (its hash is not the consent's current hash).
- **KPIs** - agreements, revocations, revocation rate and agreed contacts (on the last day of the range vs the last day of the previous period), each vs the previous period of the same length.
- **Filters** - date range, grouping and consent (all consents or one; no channel).
- **Tiles**
  - "Agreements and revocations" - stacked columns per period, or a table.
  - "Agreed contacts over time" - agreed contacts at the end of each period as a line, or a table (no total column; the values are counts on a day and do not add up).
  - "Consent text version" - per consent, agreed contacts on the current text vs an older text. Contacts on an older text may need to agree again.
  - "Consents" - all consents (the consent filter does not apply, so consents can be compared): agreed contacts on the last day of the range with the previous period value and change, and agreements and revocations in the range. A contact can agree to several consents, so shares are of all agreed contacts and do not add up to 100%. Click a consent to open its **Consent agreements** in the native **Data protection** application.
- **Open data protection** - opens the consents of the native **Data protection** application.
- Each tile has its own CSV export.

The report reads the stored agreements only and compares consent texts by hash, not by content. Deleting a contact also deletes its consent agreements, also for past days, so numbers can be lower than they were. Revoking can trigger data erasure in projects that handle it. When the consent tables do not exist, the report is empty.

### Content inventory

Shows the current state of content items (no date range, not a trend): items by content type, status, language and age, items waiting in workflow steps, and unused reusable items.

- **Filters** - content: all, pages, reusable, emails or headless (the content type's **Use for** setting). With pages, emails or headless, a **Channel** filter lists the website, email or headless channels. All and reusable have no channel filter (reusable items have no channel).
- **KPIs** - content items (with a split per content kind), content types in use, languages (with the default language), action needed (language variants unchanged in a workflow step for more than 14 days), language variants not modified in 12 months, and unused reusable items (only with all or reusable).
- **Tiles**
  - "Action needed: items in workflow steps" - shown only when variants are in a workflow step. Longest unchanged first; bars over 14 days are highlighted. Click an item to open it (or its workflow's steps when the item cannot be linked). The time an item entered its step is not stored, so days count from the last change of the language variant.
  - "Oldest content" - the 25 language variants with the oldest last change (bars over 12 months are highlighted); click an item to open it. Beside it, "Content age" (variants under 3 months, 3–6, 6–12 and over 12 months since their last change) and "Status" (see below) are stacked.
  - "Unused reusable items" - chart of unused items per content type, table of the 25 least recently changed unused items (click an item to open it in the **Content hub**). An item counts as used when another content item references it, in any language or version: through the content item selector or rich text editor (in content type fields or Page and Email Builder component properties), or through custom components with a [reference extractor](https://docs.kentico.com/documentation/developers-and-admins/customization/extend-the-administration-interface/ui-form-components/ui-form-component-reference-extractors). References that exist only in code are not tracked.
  - "Items by content type" - bar chart (up to 25 types with items) or table of every content type, including types with no items. Click a type to open it in the **Content types** application.
  - "Status" - donut chart or table of language variants by the status of their latest version: published, draft, in workflow, unpublished. Scheduled publish and unpublish counts are in the tile description.
  - "Language coverage" - items with a variant in each language vs all items, so missing translations show as the missing part.
- Each tile has its own CSV export.

Page folders are not counted. Items in all workspaces are counted. "Last change" is the modified time of the language variant's latest version. Items open where they are edited: reusable items in the **Content hub**, pages, emails and headless items in their channel application.

### Orders and revenue

Shows [digital commerce](https://docs.kentico.com/documentation/business-users/manage-commerce-stores) orders and revenue over time, orders by status, and the products with the most revenue.

- **Revenue** - the order grand total (incl. shipping and tax) as stored when the order was placed. Orders without a stored grand total count as orders with 0 revenue.
- **Currency** - orders store no currency. Amounts are formatted with the project's price formatter (`CMS.Commerce.IPriceFormatter`), the same way the native **Orders** application shows them, so they show your store's currency. Without a custom formatter the product default is used (2 decimals, no currency symbol). Amounts are not converted between currencies. Chart axes show plain numbers; tooltips and tables show formatted amounts.
- **KPIs** - orders, revenue, average order value (revenue / orders; "–" when there are no orders) and items sold (sum of item quantities), each with the change vs the previous period of the same length.
- **Filters** - date range, grouping and **Order status** (all statuses, or one of the project's order statuses from **Commerce configuration**, in their order). No channel; orders have no channel. The status filter applies to the KPIs, "Orders and revenue over time" and "Top products".
- **Tiles**
  - "Orders and revenue over time" - orders as columns (left axis) and revenue as a line (right axis), or a table.
  - "Orders by status" - donut chart or table (orders, revenue, share) of the current status of orders created in the range. Always shows every status, also with the status filter set.
  - "Top products" - the 10 products with the most revenue (sum of order item totals), grouped by SKU (by item name when an item has no SKU), with quantity, previous period revenue and change ("New" when there was none).
- **Open orders** - opens the native **Orders** application.
- Each tile has its own CSV export. CSV values are raw numbers (dot decimal separator, no grouping, no currency); amount columns are marked "(raw amount)".

Orders are grouped by the date they were created. Deleted orders are not counted. When the project does not use digital commerce (no orders, or no commerce tables), the report is empty.

### Customers

Shows [digital commerce](https://docs.kentico.com/documentation/business-users/manage-commerce-stores) customer growth, ordering and returning customers, where customers are, and the top customers.

- **Definitions**
  - **New customer** - a customer created in the range (the date the customer record was created). The order status filter does not apply.
  - **Ordering customer** - a customer with at least one order in the range.
  - **Returning customer** - an ordering customer who also has an order before the range starts (any time).
  - **Active customer** (on a day) - a customer with at least one order in the **activity window** (30, 60, 90 or 180 days, default 90) up to and including that day. A customer stops being active window days after their last order; an order exactly window days before a day no longer counts.
  - **Customer location** - the country and state of the selected address type (**Billing** or **Shipping**) on the customer's most recent order in the range. Customers whose order has no such address, or an address without a country, are counted as **Unknown**.
- **KPIs** - new customers, ordering customers, returning customers (share of ordering customers; the change is in percentage points, "pp"), active customers (on the last day of the range vs the last day of the previous period) and revenue per customer (revenue / ordering customers; "–" without ordering customers), each vs the previous period of the same length.
- **Filters** - date range, grouping, **Order status** (all statuses, or one of the project's order statuses) and **Location by** (Billing or Shipping address). No channel. The status filter applies to everything based on orders (not to new customers). The address type applies only to the location tiles. The activity window toggle is in the "Active customers" tile and also sets the "Active customers" KPI.
- **Tiles**
  - "Customer growth" - new customers as columns (left axis) and total customers at the end of each period as a line (right axis), or a table.
  - "Active customers" - active customers at the end of each day, week or month (the range end for the last one), as a line or a table, with an activity window toggle (30, 60, 90 or 180 days). Values are counts on that day, so the table and CSV have no total.
  - "Customers by country" - the 15 countries with the most ordering customers (incl. **Unknown**), with the previous period count and change.
  - "Top states and regions" - the 10 states with the most ordering customers ("State, Country"). Customers without a state are left out of this tile (they are still in "Customers by country"). Only addresses in countries that have states in Xperience (the default data has states for the USA only) can have a state.
  - "Top customers" - the 10 customers with the most revenue, orders or items (sum of item quantities), switched with the **Revenue / Orders / Items** toggle in the tile. Opens as a table with email, revenue, orders, items and the change of the ranked value vs the previous period ("New" when there was none). The name falls back to the email, then "Customer #ID". Click a customer to open it in the native **Customers** application.
- **Open customers** - opens the native **Customers** application.
- **Revenue** is the order grand total (incl. shipping and tax) as stored, formatted by the project's price formatter like in "Orders and revenue". Each tile has its own CSV export (raw numbers; amount columns are marked "(raw amount)").

Deleted customers and orders are not counted. When the project does not use digital commerce (no commerce tables), the report is empty.

### Event log

Shows events of the system [event log](https://docs.kentico.com/documentation/developers-and-admins/configuration/event-log) per type over time, and the sources, event codes and users with the most events.

- **KPIs** - all events, errors, warnings and information events in the range, each with the change vs the previous period of the same length (for example "+40% vs previous 30 days"). The change is shown as text only; a rise in errors or warnings is not colored.
- **Filters** - date range, grouping and **Event type** (all, errors, warnings or information). The event type filter applies to the tiles, not to the KPIs.
- **Tiles**
  - "Events over time" - stacked column chart or table, one series per type with fixed colors, stacked from the bottom: information, warnings, errors.
  - "Top sources" and "Top event codes" - the 10 sources and event codes with the most events, with their count in the previous period and the change ("New" when there were none).
    - "Top sources" has a toggle: **All**, **Xperience** (sources logged by the product: starting with `CMS.` or `Kentico.`, plus `WebFarmMonitor`) or **Custom** (all other sources, usually logged by project code). The CSV export has the selected list.
  - "Top users" - the 10 users with the most events, with the same previous period count and change. Events logged without a user (for example by background tasks) are one **System** row. Click a user to open it in the **Users** application.
- **Open event log** - opens the native **Event log** application to see single events.
- Each tile has its own CSV export.

The event log keeps at most the number of events in **Settings → System → Event log → Event log size**. When the log is full, the oldest events are deleted, so long ranges can show fewer events in older periods than really happened. The report shows the current limit.

### Dates and caching

Time-based reports use the server date. Results of all reports are cached for 5 minutes, so new activities, contacts, submissions, content changes, events, orders, customers, members and consent agreements can take a few minutes to appear. Select **Refresh** to load the latest numbers.

## Data retention

Contact and activity cleanup (configured in **Settings**) deletes old data. A drop in older periods can mean data was deleted, not that activity went down.

## Sample data

In the `examples/DancingGoat` project, use the **Sample data generator** application to create contacts and activities.
