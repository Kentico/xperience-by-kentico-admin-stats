using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Admin.DigitalCommerce.UIPages;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.Commerce;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.Customers;
using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Tests;

public class CustomersServiceTests
{
    private static readonly StatsQuery range = new(new(2026, 9, 1), new(2026, 9, 30), StatsGrouping.Day, null);
    private static readonly CustomersQuery query = new(range, null, CustomersAddressType.Billing);

    private FakeRepository repository = null!;
    private FakeCache cache = null!;
    private FakeClock clock = null!;
    private FakeAdminLinks adminLinks = null!;
    private FakeAmountFormatter amountFormatter = null!;
    private CustomersService service = null!;

    [SetUp]
    public void SetUp()
    {
        repository = new FakeRepository();
        cache = new FakeCache();
        clock = new FakeClock(new DateTimeOffset(2026, 9, 30, 10, 0, 0, TimeSpan.Zero));
        adminLinks = new FakeAdminLinks();
        amountFormatter = new FakeAmountFormatter();
        service = new CustomersService(repository, cache, cache, adminLinks, amountFormatter, clock);
    }

    [Test]
    public void AddressTypes_MatchProductConstants() =>
        Assert.That(Enum.GetNames<CustomersAddressType>(), Is.EqualTo(new[] { "Billing", "Shipping" }));

    [Test]
    public async Task GetReport_ReadsPreviousPeriodAndRangeInOneCall()
    {
        var result = await service.GetReport(query with { AddressType = CustomersAddressType.Shipping }, refresh: false, CancellationToken.None);

        Assert.That(repository.DataCalls, Is.EqualTo(1));
        Assert.That(
            repository.LastCall,
            Is.EqualTo((new DateOnly(2026, 8, 2), range.From, range.To, (int?)null, CustomersAddressType.Shipping, CustomersReportBuilder.TopLimit, CustomersReportBuilder.CountryLimit, 90)));
        Assert.That(result.AddressType, Is.EqualTo(CustomersAddressType.Shipping));
        Assert.That(result.ActivityWindowDays, Is.EqualTo(90));
        Assert.That(result.Statuses, Is.EqualTo(repository.Statuses));
    }

    [Test]
    public async Task GetReport_KnownStatus_IsApplied_UnknownMeansAll()
    {
        var known = await service.GetReport(query with { OrderStatusId = 4 }, refresh: false, CancellationToken.None);
        Assert.That(repository.LastCall!.Value.StatusId, Is.EqualTo(4));
        Assert.That(known.OrderStatusId, Is.EqualTo(4));

        var unknown = await service.GetReport(query with { OrderStatusId = 99 }, refresh: false, CancellationToken.None);
        Assert.That(repository.LastCall!.Value.StatusId, Is.Null);
        Assert.That(unknown.OrderStatusId, Is.Null);
    }

    [Test]
    public async Task GetReport_TablesMissing_EmptyReportWithoutStatuses()
    {
        repository.Statuses = [];
        repository.Data = CustomersReportData.Unavailable;

        var result = await service.GetReport(query with { OrderStatusId = 4 }, refresh: false, CancellationToken.None);

        Assert.That(result.CommerceAvailable, Is.False);
        Assert.That(result.OrderStatusId, Is.Null);
        Assert.That(result.Statuses, Is.Empty);
        Assert.That(result.Totals.NewCustomers.Current, Is.Zero);
    }

    [Test]
    public async Task GetReport_LinksCustomersListing_AndEachTopCustomer()
    {
        repository.Data = CustomersReportData.Empty with
        {
            Totals = CustomersTotalsRow.Empty with { OrderingCustomers = 1, Revenue = 10m, Orders = 1, Quantity = 1m },
            Customers = [new(7, "Ann", "Lee", null, 10m, 1, 1m, 0m, 0, 0m, 1, 1, 1)],
        };

        var result = await service.GetReport(query, refresh: false, CancellationToken.None);

        Assert.That(result.CustomersPath, Is.EqualTo("/CustomersList"));
        Assert.That(result.TopCustomers.ByRevenue.Items.Single().AdminPath, Is.EqualTo("/CustomerOverview/7"));
        Assert.That(result.TopCustomers.ByQuantity.Items.Single().AdminPath, Is.EqualTo("/CustomerOverview/7"));
        // One lookup per customer, also when it is in several lists.
        Assert.That(adminLinks.Pages.Count(p => p == typeof(CustomerOverview)), Is.EqualTo(1));
        Assert.That(adminLinks.Parameters.Single(), Is.EqualTo((typeof(CustomerEditSection), (object)7)));
    }

    [Test]
    public async Task GetReport_WorksWithoutLinks()
    {
        adminLinks.ReturnNull = true;
        repository.Data = CustomersReportData.Empty with { Customers = [new(7, "Ann", "Lee", null, 10m, 1, 1m, 0m, 0, 0m, 1, 1, 1)] };

        var result = await service.GetReport(query, refresh: false, CancellationToken.None);

        Assert.That(result.CustomersPath, Is.Null);
        Assert.That(result.TopCustomers.ByRevenue.Items.Single().AdminPath, Is.Null);
    }

    [Test]
    public async Task GetReport_UsesCachedData_WhenNotRefreshing_RefreshReadsAgain()
    {
        var first = await service.GetReport(query, refresh: false, CancellationToken.None);

        repository.Data = CustomersReportData.Empty with { Daily = [new(new(2026, 9, 2), 3)] };
        clock.Now = clock.Now.AddMinutes(1);
        var cached = await service.GetReport(query, refresh: false, CancellationToken.None);

        Assert.That(repository.DataCalls, Is.EqualTo(1));
        Assert.That(cached.Totals.NewCustomers.Current, Is.Zero);
        Assert.That(cached.UpdatedAt, Is.EqualTo(first.UpdatedAt));

        var refreshed = await service.GetReport(query, refresh: true, CancellationToken.None);

        Assert.That(repository.DataCalls, Is.EqualTo(2));
        Assert.That(repository.StatusCalls, Is.EqualTo(2));
        Assert.That(refreshed.Totals.NewCustomers.Current, Is.EqualTo(3m));
        Assert.That(refreshed.UpdatedAt, Is.EqualTo(clock.Now));
        Assert.That(refreshed.ByCountry.UpdatedAt, Is.EqualTo(clock.Now));
        Assert.That(refreshed.TopStates.UpdatedAt, Is.EqualTo(clock.Now));
        Assert.That(refreshed.TopCustomers.ByOrders.UpdatedAt, Is.EqualTo(clock.Now));
    }

    [Test]
    public async Task GetReport_CacheKey_SplitsOnStatusAndAddressType_NotOnChannelOrGrouping()
    {
        await service.GetReport(query, refresh: false, CancellationToken.None);
        await service.GetReport(query with { Range = range with { ChannelId = 2 } }, refresh: false, CancellationToken.None);
        await service.GetReport(query with { Range = range with { Grouping = StatsGrouping.Week } }, refresh: false, CancellationToken.None);
        await service.GetReport(query with { OrderStatusId = 99 }, refresh: false, CancellationToken.None);
        Assert.That(repository.DataCalls, Is.EqualTo(1));

        await service.GetReport(query with { OrderStatusId = 1 }, refresh: false, CancellationToken.None);
        await service.GetReport(query with { AddressType = CustomersAddressType.Shipping }, refresh: false, CancellationToken.None);
        Assert.That(repository.DataCalls, Is.EqualTo(3));

        await service.GetReport(query with { ActivityWindowDays = 30 }, refresh: false, CancellationToken.None);
        Assert.That(repository.DataCalls, Is.EqualTo(4));
        Assert.That(repository.LastCall!.Value.Window, Is.EqualTo(30));
    }

    [Test]
    public async Task GetReport_UnknownActivityWindow_IsDefault_AndSharesTheCacheKey()
    {
        await service.GetReport(query, refresh: false, CancellationToken.None);
        var result = await service.GetReport(query with { ActivityWindowDays = 45 }, refresh: false, CancellationToken.None);

        Assert.That(repository.DataCalls, Is.EqualTo(1));
        Assert.That(result.ActivityWindowDays, Is.EqualTo(CustomersActivityWindow.Default));
    }

    [Test]
    public async Task GetReport_CachesWithExpiryOnly_SharesStatusesWithOrdersRevenue()
    {
        await service.GetReport(query, refresh: false, CancellationToken.None);

        Assert.That(cache.Settings, Has.Count.EqualTo(2));
        Assert.That(cache.Settings.Select(s => s.GetCacheDependency), Is.All.Null);
        Assert.That(cache.Settings.Select(s => s.CacheMinutes), Is.All.EqualTo(StatsCache.CacheMinutes));
        Assert.That(cache.Settings.Select(s => s.CacheItemName), Has.Some.Contains("orders-revenue-statuses"));
        Assert.That(cache.Settings.Select(s => s.CacheItemName), Has.Some.Contains("customers|"));
    }

    [Test]
    public async Task GetReport_FormatsAmountsOnly_WithTheFormatter()
    {
        repository.Data = CustomersReportData.Empty with
        {
            Totals = new(0, 2, 1, 1, 0, 30m, 10m, 3, 4m),
            Customers = [new(7, "Ann", "Lee", null, 25.9m, 2, 3m, 12.5m, 1, 1m, 1, 1, 1)],
        };

        var result = await service.GetReport(query, refresh: false, CancellationToken.None);
        var top = result.TopCustomers;

        Assert.That(result.Totals.RevenuePerCustomer.CurrentText, Is.EqualTo("$15.00"));
        Assert.That(result.Totals.RevenuePerCustomer.PreviousText, Is.EqualTo("$10.00"));
        Assert.That(result.Totals.OrderingCustomers.CurrentText, Is.Null);
        Assert.That(result.Totals.ReturningShare.CurrentText, Is.Null);
        Assert.That(top.ByRevenue.Items.Single().ValueText, Is.EqualTo("$25.90"));
        Assert.That(top.ByRevenue.Items.Single().PreviousValueText, Is.EqualTo("$12.50"));
        Assert.That(top.ByRevenue.Items.Single().SecondaryValueText, Is.Null);
        Assert.That(top.ByRevenue.TotalText, Is.EqualTo("$30.00"));
        Assert.That(top.ByOrders.Items.Single().ValueText, Is.Null);
        Assert.That(top.ByOrders.Items.Single().SecondaryValueText, Is.EqualTo("$25.90"));
        Assert.That(top.ByQuantity.Items.Single().SecondaryValueText, Is.EqualTo("$25.90"));
        Assert.That(top.ByQuantity.Items.Single().TertiaryValueText, Is.Null);
    }

    private sealed class FakeAmountFormatter : IStatsAmountFormatter
    {
        public string? Format(decimal amount) =>
            "$" + amount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
    }

    private sealed class FakeRepository : ICustomersRepository
    {
        public CustomersReportData Data { get; set; } = CustomersReportData.Empty;

        public IReadOnlyList<CommerceOrderStatusOption> Statuses { get; set; } = [new(4, "Pending"), new(1, "Fulfilled")];

        public int DataCalls { get; private set; }

        public int StatusCalls { get; private set; }

        public (DateOnly PreviousFrom, DateOnly From, DateOnly To, int? StatusId, CustomersAddressType AddressType, int Limit, int CountryLimit, int Window)? LastCall { get; private set; }

        public Task<CustomersReportData> GetData(
            DateOnly previousFrom,
            DateOnly from,
            DateOnly to,
            int? orderStatusId,
            CustomersAddressType addressType,
            int limit,
            int countryLimit,
            int activityWindowDays,
            CancellationToken cancellationToken)
        {
            DataCalls++;
            LastCall = (previousFrom, from, to, orderStatusId, addressType, limit, countryLimit, activityWindowDays);
            return Task.FromResult(Data);
        }

        public Task<IReadOnlyList<CommerceOrderStatusOption>> GetStatuses(CancellationToken cancellationToken)
        {
            StatusCalls++;
            return Task.FromResult(Statuses);
        }
    }

    private sealed class FakeAdminLinks : IStatsAdminLinks
    {
        public bool ReturnNull { get; set; }

        public List<Type> Pages { get; } = [];

        public List<(Type Page, object Value)> Parameters { get; } = [];

        public string? GetPath<TPage>(PageParameterValues? parameters = null)
        {
            Pages.Add(typeof(TPage));
            if (ReturnNull)
            {
                return null;
            }

            object? value = null;
            if (parameters?.TryGetValue(typeof(CustomerEditSection), out value) == true)
            {
                Parameters.Add((typeof(CustomerEditSection), value));
            }

            return value is null ? $"/{typeof(TPage).Name}" : $"/{typeof(TPage).Name}/{value}";
        }
    }
}
