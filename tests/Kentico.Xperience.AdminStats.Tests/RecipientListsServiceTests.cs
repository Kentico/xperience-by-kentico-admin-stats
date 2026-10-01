using CMS.EmailMarketing;

using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Admin.DigitalMarketing.UIPages;
using Kentico.Xperience.AdminStats.Reports.RecipientLists;
using Kentico.Xperience.AdminStats.Shared;

using Microsoft.Extensions.Options;

namespace Kentico.Xperience.AdminStats.Tests;

public class RecipientListsServiceTests
{
    private static readonly RecipientListsQuery query = new(new(new(2026, 9, 1), new(2026, 9, 30), StatsGrouping.Day, null), null);

    private FakeRepository repository = null!;
    private FakeCache cache = null!;
    private FakeClock clock = null!;
    private FakeAdminLinks adminLinks = null!;
    private FakeBounceOptions bounceOptions = null!;
    private RecipientListsService service = null!;

    [SetUp]
    public void SetUp()
    {
        repository = new FakeRepository();
        cache = new FakeCache();
        clock = new FakeClock(new DateTimeOffset(2026, 9, 30, 10, 0, 0, TimeSpan.Zero));
        adminLinks = new FakeAdminLinks();
        bounceOptions = new FakeBounceOptions();
        service = new RecipientListsService(repository, cache, cache, adminLinks, bounceOptions, clock);
    }

    [Test]
    public async Task GetReport_ReadsPreviousPeriodAndRangeInOneCall_WithSoftBounceLimit()
    {
        bounceOptions.CurrentValue.SoftBounceLimit = 3;

        await service.GetReport(query with { RecipientListId = 2 }, refresh: false, CancellationToken.None);

        Assert.That(repository.Calls, Is.EqualTo(1));
        Assert.That(repository.LastCall, Is.EqualTo((new DateOnly(2026, 8, 2), query.Range.From, query.Range.To, (int?)2, 3)));
    }

    [Test]
    public async Task GetReport_DefaultSoftBounceLimit_IsFive()
    {
        await service.GetReport(query, refresh: false, CancellationToken.None);

        Assert.That(repository.LastCall!.Value.SoftBounceLimit, Is.EqualTo(5));
    }

    [Test]
    public async Task GetReport_TablesMissing_EmptyReport()
    {
        repository.Data = RecipientListsReportData.Unavailable;

        var result = await service.GetReport(query, refresh: false, CancellationToken.None);

        Assert.That(result.Available, Is.False);
        Assert.That(result.Totals.Subscriptions.Current, Is.Zero);
        Assert.That(result.ByList, Is.Empty);
    }

    [Test]
    public async Task GetReport_UnknownList_MeansAll()
    {
        repository.Data = RecipientListsReportData.Empty with { Lists = [new(1, "Newsletter", 0, 0, 0, 0, RecipientListStatuses.Empty)] };

        var unknown = await service.GetReport(query with { RecipientListId = 99 }, refresh: false, CancellationToken.None);
        var known = await service.GetReport(query with { RecipientListId = 1 }, refresh: false, CancellationToken.None);
        var negative = await service.GetReport(query with { RecipientListId = -1 }, refresh: false, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(unknown.RecipientListId, Is.Null);
            Assert.That(known.RecipientListId, Is.EqualTo(1));
            Assert.That(negative.RecipientListId, Is.Null);
            Assert.That(repository.LastCall!.Value.ListId, Is.Null);
        });
    }

    [Test]
    public async Task GetReport_LinksRecipientListsApp_AndEachList()
    {
        repository.Data = RecipientListsReportData.Empty with { Lists = [new(5, "Newsletter", 1, 0, 1, 0, new(1, 0, 0, 0))] };

        var result = await service.GetReport(query, refresh: false, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.RecipientListsAppPath, Is.EqualTo("/RecipientListList"));
            Assert.That(result.ByList.Single().AdminPath, Is.EqualTo("/RecipientListEditSection/5"));
        });
    }

    [Test]
    public async Task GetReport_WorksWithoutLinks()
    {
        repository.Data = RecipientListsReportData.Empty with { Lists = [new(5, "Newsletter", 1, 0, 1, 0, new(1, 0, 0, 0))] };
        adminLinks.ReturnNull = true;

        var result = await service.GetReport(query, refresh: false, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.RecipientListsAppPath, Is.Null);
            Assert.That(result.ByList.Single().AdminPath, Is.Null);
        });
    }

    [Test]
    public async Task GetReport_UsesCachedData_WhenNotRefreshing_RefreshReadsAgain()
    {
        var first = await service.GetReport(query, refresh: false, CancellationToken.None);

        repository.Data = RecipientListsReportData.Empty with { Daily = [new(new(2026, 9, 2), 3, 0, 3)] };
        clock.Now = clock.Now.AddMinutes(1);
        var cached = await service.GetReport(query, refresh: false, CancellationToken.None);

        Assert.That(repository.Calls, Is.EqualTo(1));
        Assert.That(cached.Totals.Subscriptions.Current, Is.Zero);
        Assert.That(cached.UpdatedAt, Is.EqualTo(first.UpdatedAt));

        var refreshed = await service.GetReport(query, refresh: true, CancellationToken.None);

        Assert.That(repository.Calls, Is.EqualTo(2));
        Assert.That(refreshed.Totals.Subscriptions.Current, Is.EqualTo(3m));
        Assert.That(refreshed.UpdatedAt, Is.EqualTo(clock.Now));
    }

    [Test]
    public async Task GetReport_CacheKey_SplitsOnRangeListAndBounceLimit_NotOnChannelOrGrouping()
    {
        var range = query.Range;
        await service.GetReport(query, refresh: false, CancellationToken.None);
        await service.GetReport(query with { Range = range with { ChannelId = 2 } }, refresh: false, CancellationToken.None);
        await service.GetReport(query with { Range = range with { Grouping = StatsGrouping.Month } }, refresh: false, CancellationToken.None);
        Assert.That(repository.Calls, Is.EqualTo(1));

        await service.GetReport(query with { RecipientListId = 1 }, refresh: false, CancellationToken.None);
        Assert.That(repository.Calls, Is.EqualTo(2));

        await service.GetReport(query with { Range = range with { From = new(2026, 9, 2) } }, refresh: false, CancellationToken.None);
        Assert.That(repository.Calls, Is.EqualTo(3));

        bounceOptions.CurrentValue.SoftBounceLimit = 2;
        await service.GetReport(query, refresh: false, CancellationToken.None);
        Assert.That(repository.Calls, Is.EqualTo(4));
    }

    [Test]
    public async Task GetReport_CachesWithExpiryOnly()
    {
        await service.GetReport(query, refresh: false, CancellationToken.None);

        var settings = cache.Settings.Single();
        Assert.That(settings.GetCacheDependency, Is.Null);
        Assert.That(settings.CacheMinutes, Is.EqualTo(StatsCache.CacheMinutes));
        Assert.That(settings.CacheItemName, Does.Contain("recipient-lists|"));
    }

    [Test]
    public void Filter_Normalize_DropsChannel_AndNonPositiveList()
    {
        var normalized = new RecipientListsFilter { Range = new StatsFilter { ChannelId = 3 }, RecipientListId = 0 }.Normalize(new(2026, 9, 30));

        Assert.That(normalized.Range.ChannelId, Is.Null);
        Assert.That(normalized.RecipientListId, Is.Null);
        Assert.That(new RecipientListsFilter { RecipientListId = 4 }.Normalize(new(2026, 9, 30)).RecipientListId, Is.EqualTo(4));
    }

    private sealed class FakeRepository : IRecipientListsRepository
    {
        public RecipientListsReportData Data { get; set; } = RecipientListsReportData.Empty;

        public int Calls { get; private set; }

        public (DateOnly PreviousFrom, DateOnly From, DateOnly To, int? ListId, int SoftBounceLimit)? LastCall { get; private set; }

        public Task<RecipientListsReportData> GetData(
            DateOnly previousFrom,
            DateOnly from,
            DateOnly to,
            int? listId,
            int softBounceLimit,
            CancellationToken cancellationToken)
        {
            Calls++;
            LastCall = (previousFrom, from, to, listId, softBounceLimit);
            return Task.FromResult(Data);
        }
    }

    private sealed class FakeBounceOptions : IOptionsMonitor<BouncedEmailsGlobalOptions>
    {
        public BouncedEmailsGlobalOptions CurrentValue { get; } = new();

        public BouncedEmailsGlobalOptions Get(string? name) => CurrentValue;

        public IDisposable? OnChange(Action<BouncedEmailsGlobalOptions, string?> listener) => null;
    }

    private sealed class FakeAdminLinks : IStatsAdminLinks
    {
        public bool ReturnNull { get; set; }

        public string? GetPath<TPage>(PageParameterValues? parameters = null)
        {
            if (ReturnNull)
            {
                return null;
            }

            var values = new List<object> { string.Empty, typeof(TPage).Name };
            if (parameters?.TryGetValue(typeof(RecipientListEditSection), out object? list) == true)
            {
                values.Add(list);
            }

            return string.Join('/', values);
        }
    }
}
