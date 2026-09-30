# Navigation sections

8 reports now sit flat under the Stats (Labs) application. Group them into section pages with nested navigation, like the native Content types app ("Content types > Asset configurations > Mass asset upload").

## Product pattern (decompiled `Kentico.Xperience.Admin.Base` 31.9, verified)

```csharp
[assembly: UIPage(typeof(ContentTypesApplication), "asset-configuration", typeof(AssetConfigurationSection),
    "{$...assetconfigurations$}", "@kentico/xperience-admin-base/SectionLayout", 2147473647)]
[assembly: UIPage(typeof(AssetConfigurationSection), "mass-upload-config", typeof(MassAssetUploadConfigurationEditPage), "...", "@kentico/xperience-admin-base/Edit", 100)]

[UINavigation(true)]
public sealed class AssetConfigurationSection : SecondaryMenuSectionPage
{
    // only needed to pick a non-first child; base returns routes.FirstOrDefault()
    protected override Route GetDefaultRoute(IEnumerable<Route> routes) => ...;
}
```

- `SecondaryMenuSectionPage` is `[UINavigation(false)]` by default → section pages need `[UINavigation(true)]`.
- Template: `TemplateNames.SECTION_LAYOUT`. Base `GetDefaultRoute` = first child route (by order) → auto-navigates to first child. No override needed unless verification shows otherwise.

## Grouping (order in parentheses)

| Section (slug)          | Reports (keep existing order values within section)                  |
| ----------------------- | -------------------------------------------------------------------- |
| Contacts (`contacts`) 100 | Activity counts, Top pages, New contacts, Form submissions         |
| Content (`content`) 200   | Content inventory                                                  |
| Commerce (`commerce`) 300 | Orders and revenue, Customers                                      |
| System (`system`) 400     | Event log                                                          |

Section icons: pick fitting `Icons.*` (e.g. match the native app categories); section pages may not show icons in nav — check.

## Requirements

- New section pages in `src/Kentico.Xperience.AdminStats/Admin/` (e.g. `StatsContactsSection`, ... or one file `StatsSections.cs` — follow existing one-page-per-file style). Report pages change `parentType` only; slugs, names, templates, permissions unchanged.
- The application page's default route should land on the first section → its first report (verify it works two levels deep).
- **Permissions**: sections have no permission of their own; per-report permissions unchanged. Verify in the app/by reading the product code:
  1. Role with only e.g. `Customers` permission: nav shows only Commerce > Customers; opening the app / Commerce section lands on Customers, not a 403 on Orders and revenue. If `GetDefaultRoute` receives unfiltered routes, override in a shared base section class that picks the first route the user may open (`IUIPermissionEvaluator`), no hardcoded slugs.
  2. Sections whose children are all denied are hidden (or explain what the product does).
- Links: all internal links must still resolve — `IStatsAdminLinks`, `IPageLinkGenerator.GetPath<T>()`, client `PagePath`, any hardcoded `admin-stats/...` paths in client, tests or docs. Grep for them.
- Tests: update any tests touching page registration/paths; add tests for any permission-aware default route logic.
- `docs/Usage-Guide.md`: describe sections + which report is where. README if it mentions nav.
- Follow `dotnet format`. If it inserts a throw into a switch, a case is uncovered: keep a throw (`ArgumentOutOfRangeException`, see `Shared/StatsPeriods.cs`) or return a handled error state — never a silent default.
- Nothing DancingGoat-specific in `src/`. Do not commit.

## Done when

- `dotnet build` (library + tests), `dotnet test`, `dotnet format --verify-no-changes`, client `npm run typecheck` + `npm run build` pass.
- Visual check in DancingGoat admin if possible (the app runs via `dotnet watch`; server changes may need a restart — say if you couldn't). Otherwise say not checked.
- Report back concise: files changed, whether a default-route override was needed and why, permission verification results, link changes, what was / was not checked.
