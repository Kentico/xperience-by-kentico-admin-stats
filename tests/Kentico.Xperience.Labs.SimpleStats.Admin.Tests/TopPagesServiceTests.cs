using CMS.Helpers;

using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.TopPages;
using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Tests;

public class TopPagesServiceTests
{
    private static readonly StatsQuery query = new(new(2026, 9, 1), new(2026, 9, 30), StatsGrouping.Day, null);

    private FakeRepository repository = null!;
    private FakeCache cache = null!;
    private FakeClock clock = null!;
    private TopPagesService service = null!;

    [SetUp]
    public void SetUp()
    {
        repository = new FakeRepository();
        cache = new FakeCache();
        clock = new FakeClock(new DateTimeOffset(2026, 9, 30, 10, 0, 0, TimeSpan.Zero));
        service = new TopPagesService(repository, cache, cache, clock);
    }

    [Test]
    public async Task GetReport_PassesFilterAndLimitToRepository()
    {
        await service.GetReport(query with { ChannelId = 3 }, refresh: false, CancellationToken.None);

        Assert.That(repository.LastCall, Is.EqualTo((query.From, query.To, (int?)3, TopPagesService.Limit)));
    }

    [Test]
    public async Task GetReport_UsesCachedData_WhenNotRefreshing()
    {
        var first = await service.GetReport(query, refresh: false, CancellationToken.None);

        repository.Data = new([new("https://example.com/", 5, 3, null)], 5, 1);
        clock.Now = clock.Now.AddMinutes(1);
        var second = await service.GetReport(query, refresh: false, CancellationToken.None);

        Assert.That(repository.Calls, Is.EqualTo(1));
        Assert.That(second.Items, Is.Empty);
        Assert.That(second.UpdatedAt, Is.EqualTo(first.UpdatedAt));
    }

    [Test]
    public async Task GetReport_Refresh_ReadsDatabaseAndCachesFreshResult()
    {
        await service.GetReport(query, refresh: false, CancellationToken.None);

        repository.Data = new([new("https://example.com/", 5, 3, null)], 8, 2);
        clock.Now = clock.Now.AddMinutes(1);
        var refreshed = await service.GetReport(query, refresh: true, CancellationToken.None);

        Assert.That(refreshed.Total, Is.EqualTo(8));
        Assert.That(refreshed.ItemCount, Is.EqualTo(2));
        Assert.That(refreshed.Items.Single().Share, Is.EqualTo(5d / 8).Within(1e-9));
        Assert.That(refreshed.UpdatedAt, Is.EqualTo(clock.Now));

        var afterRefresh = await service.GetReport(query, refresh: false, CancellationToken.None);

        Assert.That(repository.Calls, Is.EqualTo(2));
        Assert.That(afterRefresh.Total, Is.EqualTo(8));
        Assert.That(afterRefresh.UpdatedAt, Is.EqualTo(refreshed.UpdatedAt));
    }

    [Test]
    public async Task GetReport_KeysCacheByRangeAndChannel_NotGrouping()
    {
        await service.GetReport(query, refresh: false, CancellationToken.None);
        await service.GetReport(query with { Grouping = StatsGrouping.Month }, refresh: false, CancellationToken.None);
        Assert.That(repository.Calls, Is.EqualTo(1));

        await service.GetReport(query with { ChannelId = 2 }, refresh: false, CancellationToken.None);
        await service.GetReport(query with { From = new(2026, 8, 1) }, refresh: false, CancellationToken.None);
        Assert.That(repository.Calls, Is.EqualTo(3));
    }

    [Test]
    public async Task GetReport_Refresh_OnlyDropsQueriedFilter()
    {
        var otherQuery = query with { ChannelId = 2 };
        await service.GetReport(query, refresh: false, CancellationToken.None);
        await service.GetReport(otherQuery, refresh: false, CancellationToken.None);

        await service.GetReport(query, refresh: true, CancellationToken.None);
        await service.GetReport(otherQuery, refresh: false, CancellationToken.None);

        Assert.That(repository.Calls, Is.EqualTo(3));
    }

    [Test]
    public async Task GetReport_CachesWithExpiryOnly()
    {
        await service.GetReport(query, refresh: false, CancellationToken.None);

        Assert.That(cache.Settings, Is.Not.Empty);
        Assert.That(cache.Settings, Has.All.Matches<CacheSettings>(s => s.GetCacheDependency == null));
        Assert.That(cache.Settings.Select(s => s.CacheMinutes), Has.All.EqualTo(StatsCache.CacheMinutes));
    }

    [Test]
    public async Task GetReport_UsesDifferentCacheKeyThanActivityCounts()
    {
        await service.GetReport(query, refresh: false, CancellationToken.None);

        Assert.That(cache.Settings.Single().CacheItemName, Does.Contain("top-pages"));
    }

    private sealed class FakeRepository : ITopPagesRepository
    {
        public TopPagesData Data { get; set; } = TopPagesData.Empty;

        public int Calls { get; private set; }

        public (DateOnly From, DateOnly To, int? ChannelId, int Limit)? LastCall { get; private set; }

        public Task<TopPagesData> GetTopPages(DateOnly from, DateOnly to, int? channelId, int limit, CancellationToken cancellationToken)
        {
            Calls++;
            LastCall = (from, to, channelId, limit);
            return Task.FromResult(Data);
        }
    }
}
