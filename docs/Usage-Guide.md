# Usage Guide

## Setup

1. Install the `Kentico.Xperience.AdminStats` NuGet package in your Xperience by Kentico web project.
2. Run the application. No service registration is needed; the library registers its services and admin UI automatically.

## Permissions

The library adds the **Stats (Labs)** application to the **Digital marketing** category.

- Administrators see it by default.
- For other roles, open **Role management**, select the role, and give it the **View** permission for **Stats (Labs)**.

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

### Dates and caching

All reports: dates use the server date. Results are cached for 5 minutes, so new activities, contacts and submissions can take a few minutes to appear. Select **Refresh** to load the latest numbers.

## Data retention

Contact and activity cleanup (configured in **Settings**) deletes old data. A drop in older periods can mean data was deleted, not that activity went down.

## Sample data

In the `examples/DancingGoat` project, use the **Sample data generator** application to create contacts and activities.
