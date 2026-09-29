using CMS.Helpers;

using Kentico.Xperience.AdminStats.Reports.ActivityCounts;
using Kentico.Xperience.AdminStats.Shared;

namespace Kentico.Xperience.AdminStats.Tests;

public class ActivityCountsServiceTests
{
    private static readonly StatsQuery query = new(new(2026, 9, 1), new(2026, 9, 3), StatsGrouping.Day, null);

    private FakeRepository repository = null!;
    private FakeCache cache = null!;
    private FakeClock clock = null!;
    private ActivityCountsService service = null!;

    [SetUp]
    public void SetUp()
    {
        repository = new FakeRepository();
        cache = new FakeCache();
        clock = new FakeClock(new DateTimeOffset(2026, 9, 3, 10, 0, 0, TimeSpan.Zero));
        service = new ActivityCountsService(repository, cache, cache, clock);
    }

    [Test]
    public async Task GetReport_UsesCachedCounts_WhenNotRefreshing()
    {
        var first = await service.GetReport(query, refresh: false, CancellationToken.None);

        repository.Rows = [new("pagevisit", new(2026, 9, 2), 5)];
        clock.Now = clock.Now.AddMinutes(1);
        var second = await service.GetReport(query, refresh: false, CancellationToken.None);

        Assert.That(repository.DailyCountsCalls, Is.EqualTo(1));
        Assert.That(second.Total, Is.Zero);
        Assert.That(second.UpdatedAt, Is.EqualTo(first.UpdatedAt));
    }

    [Test]
    public async Task GetReport_Refresh_ReadsDatabaseAndCachesFreshResult()
    {
        // Empty result cached before activities exist.
        await service.GetReport(query, refresh: false, CancellationToken.None);

        repository.Rows = [new("pagevisit", new(2026, 9, 2), 5)];
        repository.DisplayNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["pagevisit"] = "Page visit" };
        clock.Now = clock.Now.AddMinutes(1);
        var refreshed = await service.GetReport(query, refresh: true, CancellationToken.None);

        Assert.That(refreshed.Total, Is.EqualTo(5));
        Assert.That(refreshed.Series[0].DisplayName, Is.EqualTo("Page visit"));
        Assert.That(refreshed.UpdatedAt, Is.EqualTo(clock.Now));

        var afterRefresh = await service.GetReport(query, refresh: false, CancellationToken.None);

        Assert.That(repository.DailyCountsCalls, Is.EqualTo(2));
        Assert.That(repository.DisplayNamesCalls, Is.EqualTo(2));
        Assert.That(afterRefresh.Total, Is.EqualTo(5));
        Assert.That(afterRefresh.UpdatedAt, Is.EqualTo(refreshed.UpdatedAt));
    }

    [Test]
    public async Task GetReport_Refresh_OnlyDropsQueriedRange()
    {
        var otherQuery = query with { From = new(2026, 8, 1) };
        await service.GetReport(query, refresh: false, CancellationToken.None);
        await service.GetReport(otherQuery, refresh: false, CancellationToken.None);

        await service.GetReport(query, refresh: true, CancellationToken.None);
        await service.GetReport(otherQuery, refresh: false, CancellationToken.None);

        Assert.That(repository.DailyCountsCalls, Is.EqualTo(3));
    }

    [Test]
    public async Task GetReport_GroupingChange_ReusesCachedCounts()
    {
        await service.GetReport(query, refresh: false, CancellationToken.None);
        await service.GetReport(query with { Grouping = StatsGrouping.Week }, refresh: false, CancellationToken.None);

        Assert.That(repository.DailyCountsCalls, Is.EqualTo(1));
    }

    [Test]
    public async Task GetReport_CachesWithExpiryOnly()
    {
        await service.GetReport(query, refresh: false, CancellationToken.None);

        Assert.That(cache.Settings, Is.Not.Empty);
        Assert.That(cache.Settings, Has.All.Matches<CacheSettings>(s => s.GetCacheDependency == null));
        Assert.That(cache.Settings.Select(s => s.CacheMinutes), Has.All.EqualTo(ActivityCountsService.CacheMinutes));
    }

    private sealed class FakeRepository : IActivityCountsRepository
    {
        public IReadOnlyList<ActivityDailyCount> Rows { get; set; } = [];

        public IReadOnlyDictionary<string, string> DisplayNames { get; set; } = new Dictionary<string, string>();

        public int DailyCountsCalls { get; private set; }

        public int DisplayNamesCalls { get; private set; }

        public Task<IReadOnlyList<ActivityDailyCount>> GetDailyCounts(DateOnly from, DateOnly to, int? channelId, CancellationToken cancellationToken)
        {
            DailyCountsCalls++;
            return Task.FromResult(Rows);
        }

        public Task<IReadOnlyDictionary<string, string>> GetActivityTypeDisplayNames(CancellationToken cancellationToken)
        {
            DisplayNamesCalls++;
            return Task.FromResult(DisplayNames);
        }
    }

    /// <summary>
    /// Dictionary cache keyed by <see cref="CacheSettings.CacheItemName"/>. Ignores expiry and dependencies.
    /// </summary>
    private sealed class FakeCache : IProgressiveCache, IStatsCacheInvalidator
    {
        private readonly Dictionary<string, object?> items = new(StringComparer.OrdinalIgnoreCase);

        public List<CacheSettings> Settings { get; } = [];

        public TData Load<TData>(Func<CacheSettings, TData> loadDataFunc, CacheSettings settings) =>
            throw new NotSupportedException();

        public Task<TData> LoadAsync<TData>(Func<CacheSettings, Task<TData>> loadDataFuncAsync, CacheSettings settings) =>
            LoadAsync((s, _) => loadDataFuncAsync(s), settings, CancellationToken.None);

        public async Task<TData> LoadAsync<TData>(Func<CacheSettings, CancellationToken, Task<TData>> loadDataFuncAsync, CacheSettings settings, CancellationToken cancellationToken)
        {
            Settings.Add(settings);

            if (items.TryGetValue(settings.CacheItemName, out object? cached))
            {
                return (TData)cached!;
            }

            var data = await loadDataFuncAsync(settings, cancellationToken);
            items[settings.CacheItemName] = data;
            return data;
        }

        public void Remove(CacheSettings settings) => items.Remove(settings.CacheItemName);
    }

    private sealed class FakeClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
