using CMS.Core;

using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Admin.Base.UIPages;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.EventLog;
using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Tests;

public class EventLogReportServiceTests
{
    private static readonly StatsQuery range = new(new(2026, 9, 1), new(2026, 9, 30), StatsGrouping.Day, null);
    private static readonly EventLogQuery query = new(range, null);

    private FakeRepository repository = null!;
    private FakeSettings settings = null!;
    private FakeCache cache = null!;
    private FakeClock clock = null!;
    private FakeAdminLinks adminLinks = null!;
    private EventLogReportService service = null!;

    [SetUp]
    public void SetUp()
    {
        repository = new FakeRepository();
        settings = new FakeSettings { Value = "10000" };
        cache = new FakeCache();
        clock = new FakeClock(new DateTimeOffset(2026, 9, 30, 10, 0, 0, TimeSpan.Zero));
        adminLinks = new FakeAdminLinks();
        service = new EventLogReportService(repository, settings, cache, cache, adminLinks, clock);
    }

    [Test]
    public async Task GetReport_ReadsPreviousPeriodAndRangeInOneCall()
    {
        await service.GetReport(query with { EventType = "E" }, refresh: false, CancellationToken.None);

        Assert.That(repository.Calls, Is.EqualTo(1));
        Assert.That(repository.LastCall, Is.EqualTo((new DateOnly(2026, 8, 2), range.From, range.To, (string?)"E", EventLogReportBuilder.TopLimit)));
    }

    [Test]
    public async Task GetReport_ReadsLogSizeSetting()
    {
        var result = await service.GetReport(query, refresh: false, CancellationToken.None);

        Assert.That(settings.LastKey, Is.EqualTo("CMSLogSize"));
        Assert.That(result.LogSizeLimit, Is.EqualTo(10000));
    }

    [TestCase("0", 0)]
    [TestCase("", null)]
    [TestCase("abc", null)]
    [TestCase("-5", null)]
    public async Task GetReport_LogSizeSetting_ZeroKept_InvalidIsNull(string value, int? expected)
    {
        settings.Value = value;

        var result = await service.GetReport(query, refresh: false, CancellationToken.None);

        Assert.That(result.LogSizeLimit, Is.EqualTo(expected));
    }

    [Test]
    public async Task GetReport_LinksEventLogAndExistingUsers()
    {
        repository.Data = EventLogReportData.Empty with
        {
            Daily = [new("I", new(2026, 9, 2), 3)],
            Users = new([new(53, "administrator", true, 3, 1)], 1),
        };

        var result = await service.GetReport(query, refresh: false, CancellationToken.None);

        Assert.That(result.EventLogPath, Is.EqualTo("/EventLogList"));
        Assert.That(result.TopUsers.Items.Single().AdminPath, Is.EqualTo("/UserEdit/53"));
        Assert.That(adminLinks.Calls, Does.Contain((typeof(UserEdit), (Type?)typeof(UserEditSection), (object?)53)));
    }

    [Test]
    public async Task GetReport_WorksWithoutLinks()
    {
        repository.Data = EventLogReportData.Empty with { Users = new([new(53, "administrator", true, 3, 1)], 1) };
        adminLinks.ReturnNull = true;

        var result = await service.GetReport(query, refresh: false, CancellationToken.None);

        Assert.That(result.EventLogPath, Is.Null);
        Assert.That(result.TopUsers.Items.Single().AdminPath, Is.Null);
    }

    [Test]
    public async Task GetReport_UsesCachedData_WhenNotRefreshing()
    {
        var first = await service.GetReport(query, refresh: false, CancellationToken.None);

        repository.Data = EventLogReportData.Empty with { Daily = [new("E", new(2026, 9, 2), 3)] };
        clock.Now = clock.Now.AddMinutes(1);
        var second = await service.GetReport(query, refresh: false, CancellationToken.None);

        Assert.That(repository.Calls, Is.EqualTo(1));
        Assert.That(second.TotalComparison.Current, Is.Zero);
        Assert.That(second.UpdatedAt, Is.EqualTo(first.UpdatedAt));
    }

    [Test]
    public async Task GetReport_Refresh_ReadsDatabaseAndSetsUpdatedAtEverywhere()
    {
        await service.GetReport(query, refresh: false, CancellationToken.None);

        repository.Data = EventLogReportData.Empty with { Daily = [new("E", new(2026, 9, 2), 3)] };
        clock.Now = clock.Now.AddMinutes(1);
        var refreshed = await service.GetReport(query, refresh: true, CancellationToken.None);

        Assert.That(repository.Calls, Is.EqualTo(2));
        Assert.That(refreshed.TotalComparison.Current, Is.EqualTo(3));
        Assert.That(refreshed.UpdatedAt, Is.EqualTo(clock.Now));
        Assert.That(refreshed.Trend.UpdatedAt, Is.EqualTo(clock.Now));
        Assert.That(refreshed.TopSources.UpdatedAt, Is.EqualTo(clock.Now));
        Assert.That(refreshed.TopCodes.UpdatedAt, Is.EqualTo(clock.Now));
        Assert.That(refreshed.TopUsers.UpdatedAt, Is.EqualTo(clock.Now));
    }

    [Test]
    public async Task GetReport_CacheKey_SplitsOnType_NotOnChannelOrGrouping()
    {
        await service.GetReport(query, refresh: false, CancellationToken.None);
        var withChannel = await service.GetReport(query with { Range = range with { ChannelId = 2 } }, refresh: false, CancellationToken.None);
        var byWeek = await service.GetReport(query with { Range = range with { Grouping = StatsGrouping.Week } }, refresh: false, CancellationToken.None);

        Assert.That(repository.Calls, Is.EqualTo(1));
        Assert.That(withChannel.Trend.ChannelId, Is.Null);
        Assert.That(byWeek.Trend.Grouping, Is.EqualTo(StatsGrouping.Week));

        await service.GetReport(query with { EventType = "W" }, refresh: false, CancellationToken.None);
        Assert.That(repository.Calls, Is.EqualTo(2));
    }

    [Test]
    public async Task GetReport_CachesWithExpiryOnly_UnderOwnKey()
    {
        await service.GetReport(query, refresh: false, CancellationToken.None);

        var cacheSettings = cache.Settings.Single();
        Assert.That(cacheSettings.GetCacheDependency, Is.Null);
        Assert.That(cacheSettings.CacheMinutes, Is.EqualTo(StatsCache.CacheMinutes));
        Assert.That(cacheSettings.CacheItemName, Does.Contain("event-log"));
    }

    private sealed class FakeRepository : IEventLogRepository
    {
        public EventLogReportData Data { get; set; } = EventLogReportData.Empty;

        public int Calls { get; private set; }

        public (DateOnly PreviousFrom, DateOnly From, DateOnly To, string? EventType, int Limit)? LastCall { get; private set; }

        public Task<EventLogReportData> GetData(DateOnly previousFrom, DateOnly from, DateOnly to, string? eventType, int limit, CancellationToken cancellationToken)
        {
            Calls++;
            LastCall = (previousFrom, from, to, eventType, limit);
            return Task.FromResult(Data);
        }
    }

    private sealed class FakeSettings : ISettingsService
    {
        public string Value { get; set; } = string.Empty;

        public string? LastKey { get; private set; }

        public string this[string keyName]
        {
            get
            {
                LastKey = keyName;
                return Value;
            }
        }
    }

    private sealed class FakeAdminLinks : IStatsAdminLinks
    {
        public bool ReturnNull { get; set; }

        public List<(Type Page, Type? ParameterPage, object? Value)> Calls { get; } = [];

        public string? GetPath<TPage>(PageParameterValues? parameters = null)
        {
            if (parameters is null || !parameters.Any())
            {
                Calls.Add((typeof(TPage), null, null));
                return ReturnNull ? null : $"/{typeof(TPage).Name}";
            }

            var parameter = parameters.Single();
            Calls.Add((typeof(TPage), parameter.Key, parameter.Value));
            return ReturnNull ? null : $"/{typeof(TPage).Name}/{parameter.Value}";
        }
    }
}
