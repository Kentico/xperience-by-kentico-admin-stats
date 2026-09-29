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

Dates use the server date. Results are cached for 5 minutes, so new activities can take a few minutes to appear. Select **Refresh** to load the latest numbers.

## Data retention

Contact and activity cleanup (configured in **Settings**) deletes old data. A drop in older periods can mean data was deleted, not that activity went down.

## Sample data

In the `examples/DancingGoat` project, use the **Sample data generator** application to create contacts and activities.
