using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Admin.DigitalMarketing.UIPages;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.FormSubmissions;
using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Tests;

public class FormSubmissionsServiceTests
{
    private static readonly StatsQuery query = new(new(2026, 9, 1), new(2026, 9, 30), StatsGrouping.Day, null);

    private static readonly FormDefinition[] forms = [new(4, "Contact", "Contact us", "Form_Contact")];

    private FakeRepository repository = null!;
    private FakeCache cache = null!;
    private FakeClock clock = null!;
    private FakeAdminLinks adminLinks = null!;
    private FormSubmissionsService service = null!;

    [SetUp]
    public void SetUp()
    {
        repository = new FakeRepository();
        cache = new FakeCache();
        clock = new FakeClock(new DateTimeOffset(2026, 9, 30, 10, 0, 0, TimeSpan.Zero));
        adminLinks = new FakeAdminLinks();
        service = new FormSubmissionsService(repository, cache, cache, adminLinks, clock);
    }

    [Test]
    public async Task GetReport_ReadsPreviousPeriodAndRangeInOneCall()
    {
        await service.GetReport(query, refresh: false, CancellationToken.None);

        Assert.That(repository.Calls, Is.EqualTo(1));
        Assert.That(repository.LastCall, Is.EqualTo((new DateOnly(2026, 8, 2), query.To)));
    }

    [Test]
    public async Task GetReport_ComparesTotalWithPreviousPeriod()
    {
        repository.Data = new(forms, [new(4, new(2026, 8, 2), 4), new(4, new(2026, 9, 2), 5)]);

        var result = await service.GetReport(query, refresh: false, CancellationToken.None);

        Assert.That(result.Total, Is.EqualTo(5));
        Assert.That(result.TotalComparison.Previous, Is.EqualTo(4));
        Assert.That(result.TotalComparison.Change, Is.EqualTo(0.25).Within(1e-9));
    }

    [Test]
    public async Task GetReport_LinksFormsToSubmissionsTab()
    {
        repository.Data = new(forms, []);

        var result = await service.GetReport(query, refresh: false, CancellationToken.None);

        Assert.That(result.Forms.Items.Single().AdminPath, Is.EqualTo("/FormSubmissionsTab/4"));
        Assert.That(adminLinks.Calls.Single(), Is.EqualTo((typeof(FormSubmissionsTab), typeof(FormEditSection), (object)4)));
    }

    [Test]
    public async Task GetReport_WorksWithoutLinks()
    {
        repository.Data = new(forms, []);
        adminLinks.ReturnNull = true;

        var result = await service.GetReport(query, refresh: false, CancellationToken.None);

        Assert.That(result.Forms.Items.Single().AdminPath, Is.Null);
    }

    [Test]
    public async Task GetReport_UsesCachedData_WhenNotRefreshing()
    {
        var first = await service.GetReport(query, refresh: false, CancellationToken.None);

        repository.Data = new(forms, [new(4, new(2026, 9, 2), 3)]);
        clock.Now = clock.Now.AddMinutes(1);
        var second = await service.GetReport(query, refresh: false, CancellationToken.None);

        Assert.That(repository.Calls, Is.EqualTo(1));
        Assert.That(second.Total, Is.Zero);
        Assert.That(second.UpdatedAt, Is.EqualTo(first.UpdatedAt));
    }

    [Test]
    public async Task GetReport_Refresh_ReadsDatabaseAndSetsUpdatedAtEverywhere()
    {
        await service.GetReport(query, refresh: false, CancellationToken.None);

        repository.Data = new(forms, [new(4, new(2026, 9, 2), 3)]);
        clock.Now = clock.Now.AddMinutes(1);
        var refreshed = await service.GetReport(query, refresh: true, CancellationToken.None);

        Assert.That(repository.Calls, Is.EqualTo(2));
        Assert.That(refreshed.Total, Is.EqualTo(3));
        Assert.That(refreshed.UpdatedAt, Is.EqualTo(clock.Now));
        Assert.That(refreshed.Trend.UpdatedAt, Is.EqualTo(clock.Now));
        Assert.That(refreshed.Forms.UpdatedAt, Is.EqualTo(clock.Now));
    }

    [Test]
    public async Task GetReport_IgnoresChannelAndGrouping_InCacheKeyAndResult()
    {
        await service.GetReport(query, refresh: false, CancellationToken.None);
        var withChannel = await service.GetReport(query with { ChannelId = 2 }, refresh: false, CancellationToken.None);
        var byWeek = await service.GetReport(query with { Grouping = StatsGrouping.Week }, refresh: false, CancellationToken.None);

        Assert.That(repository.Calls, Is.EqualTo(1));
        Assert.That(withChannel.Trend.ChannelId, Is.Null);
        Assert.That(withChannel.Forms.ChannelId, Is.Null);
        Assert.That(byWeek.Trend.Grouping, Is.EqualTo(StatsGrouping.Week));
    }

    [Test]
    public async Task GetReport_CachesWithExpiryOnly_UnderOwnKey()
    {
        await service.GetReport(query, refresh: false, CancellationToken.None);

        var settings = cache.Settings.Single();
        Assert.That(settings.GetCacheDependency, Is.Null);
        Assert.That(settings.CacheMinutes, Is.EqualTo(StatsCache.CacheMinutes));
        Assert.That(settings.CacheItemName, Does.Contain("form-submissions"));
    }

    private sealed class FakeRepository : IFormSubmissionsRepository
    {
        public FormSubmissionsData Data { get; set; } = FormSubmissionsData.Empty;

        public int Calls { get; private set; }

        public (DateOnly From, DateOnly To)? LastCall { get; private set; }

        public Task<FormSubmissionsData> GetData(DateOnly from, DateOnly to, CancellationToken cancellationToken)
        {
            Calls++;
            LastCall = (from, to);
            return Task.FromResult(Data);
        }
    }

    private sealed class FakeAdminLinks : IStatsAdminLinks
    {
        public bool ReturnNull { get; set; }

        public List<(Type Page, Type ParameterPage, object Value)> Calls { get; } = [];

        public string? GetPath<TPage>(PageParameterValues? parameters = null)
        {
            var parameter = parameters!.Single();
            Calls.Add((typeof(TPage), parameter.Key, parameter.Value));
            return ReturnNull ? null : $"/{typeof(TPage).Name}/{parameter.Value}";
        }
    }
}
