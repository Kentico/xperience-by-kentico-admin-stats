namespace Kentico.Xperience.AdminStats.Admin;

/// <summary>
/// Custom permissions of the "Stats (Labs)" application, one per report page.
/// Names are stored in role assignments, so do not change them.
/// </summary>
public static class StatsPermissions
{
    private const string PREFIX = "Kentico.Xperience.AdminStats.";

    public const string ACTIVITY_COUNTS = PREFIX + "ActivityCounts";
    public const string ACTIVITY_COUNTS_DISPLAY_NAME = "Activity counts";

    public const string TOP_PAGES = PREFIX + "TopPages";
    public const string TOP_PAGES_DISPLAY_NAME = "Top pages";

    public const string NEW_CONTACTS = PREFIX + "NewContacts";
    public const string NEW_CONTACTS_DISPLAY_NAME = "New contacts";

    public const string FORM_SUBMISSIONS = PREFIX + "FormSubmissions";
    public const string FORM_SUBMISSIONS_DISPLAY_NAME = "Form submissions";

    public const string CONTENT_INVENTORY = PREFIX + "ContentInventory";
    public const string CONTENT_INVENTORY_DISPLAY_NAME = "Content inventory";

    public const string EVENT_LOG = PREFIX + "EventLog";
    public const string EVENT_LOG_DISPLAY_NAME = "Event log";

    public const string ORDERS_REVENUE = PREFIX + "OrdersRevenue";
    public const string ORDERS_REVENUE_DISPLAY_NAME = "Orders and revenue";
}
