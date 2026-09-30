using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Admin.DigitalMarketing.UIPages;
using Kentico.Xperience.AdminStats.Reports.Consents;
using Kentico.Xperience.AdminStats.Shared;

namespace Kentico.Xperience.AdminStats.Tests;

public class ConsentsServiceTests
{
    private static readonly ConsentsQuery query = new(new(new(2026, 9, 1), new(2026, 9, 30), StatsGrouping.Day, null), null);

    private FakeRepository repository = null!;
    private FakeCache cache = null!;
    private FakeClock clock = null!;
    private FakeAdminLinks adminLinks = null!;
    private ConsentsService service = null!;

    [SetUp]
    public void SetUp()
    {
        repository = new FakeRepository();
        cache = new FakeCache();
        clock = new FakeClock(new DateTimeOffset(2026, 9, 30, 10, 0, 0, TimeSpan.Zero));
        adminLinks = new FakeAdminLinks();
        service = new ConsentsService(repository, cache, cache, adminLinks, clock);
    }

    [Test]
    public async Task GetReport_ReadsPreviousPeriodAndRangeInOneCall()
    {
        await service.GetReport(query with { ConsentId = 2 }, refresh: false, CancellationToken.None);

        Assert.That(repository.Calls, Is.EqualTo(1));
        Assert.That(repository.LastCall, Is.EqualTo((new DateOnly(2026, 8, 2), query.Range.From, query.Range.To, (int?)2)));
    }

    [Test]
    public async Task GetReport_TablesMissing_EmptyReport()
    {
        repository.Data = ConsentsReportData.Unavailable;

        var result = await service.GetReport(query, refresh: false, CancellationToken.None);

        Assert.That(result.Available, Is.False);
        Assert.That(result.Totals.Agreements.Current, Is.Zero);
        Assert.That(result.ByConsent.Items, Is.Empty);
    }

    [Test]
    public async Task GetReport_UnknownConsent_MeansAll()
    {
        repository.Data = ConsentsReportData.Empty with { Consents = [new(1, "Tracking", 0, 0, 0, 0, 0, 0)] };

        var unknown = await service.GetReport(query with { ConsentId = 99 }, refresh: false, CancellationToken.None);
        var known = await service.GetReport(query with { ConsentId = 1 }, refresh: false, CancellationToken.None);
        var negative = await service.GetReport(query with { ConsentId = -1 }, refresh: false, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(unknown.ConsentId, Is.Null);
            Assert.That(known.ConsentId, Is.EqualTo(1));
            Assert.That(negative.ConsentId, Is.Null);
            Assert.That(repository.LastCall!.Value.ConsentId, Is.Null);
        });
    }

    [Test]
    public async Task GetReport_LinksDataProtection_AndEachConsentsAgreements()
    {
        repository.Data = ConsentsReportData.Empty with
        {
            Consents = [new(5, "Tracking", 1, 0, 0, 1, 0, 0)],
            DefaultLanguageName = "en",
        };

        var result = await service.GetReport(query, refresh: false, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.DataProtectionPath, Is.EqualTo("/ConsentList/en"));
            Assert.That(result.ByConsent.Items.Single().AdminPath, Is.EqualTo("/ConsentAgreementList/en/5"));
        });
    }

    [Test]
    public async Task GetReport_WorksWithoutLinks()
    {
        repository.Data = ConsentsReportData.Empty with { Consents = [new(5, "Tracking", 1, 0, 0, 1, 0, 0)], DefaultLanguageName = "en" };
        adminLinks.ReturnNull = true;

        var withoutPages = await service.GetReport(query, refresh: true, CancellationToken.None);

        adminLinks.ReturnNull = false;
        repository.Data = repository.Data with { DefaultLanguageName = null };
        var withoutLanguage = await service.GetReport(query, refresh: true, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(withoutPages.DataProtectionPath, Is.Null);
            Assert.That(withoutPages.ByConsent.Items.Single().AdminPath, Is.Null);
            Assert.That(withoutLanguage.DataProtectionPath, Is.Null);
            Assert.That(withoutLanguage.ByConsent.Items.Single().AdminPath, Is.Null);
        });
    }

    [Test]
    public async Task GetReport_UsesCachedData_WhenNotRefreshing_RefreshReadsAgain()
    {
        var first = await service.GetReport(query, refresh: false, CancellationToken.None);

        repository.Data = ConsentsReportData.Empty with { Daily = [new(new(2026, 9, 2), 3, 0, 3)] };
        clock.Now = clock.Now.AddMinutes(1);
        var cached = await service.GetReport(query, refresh: false, CancellationToken.None);

        Assert.That(repository.Calls, Is.EqualTo(1));
        Assert.That(cached.Totals.Agreements.Current, Is.Zero);
        Assert.That(cached.UpdatedAt, Is.EqualTo(first.UpdatedAt));

        var refreshed = await service.GetReport(query, refresh: true, CancellationToken.None);

        Assert.That(repository.Calls, Is.EqualTo(2));
        Assert.That(refreshed.Totals.Agreements.Current, Is.EqualTo(3m));
        Assert.That(refreshed.UpdatedAt, Is.EqualTo(clock.Now));
        Assert.That(refreshed.ByConsent.UpdatedAt, Is.EqualTo(clock.Now));
    }

    [Test]
    public async Task GetReport_CacheKey_SplitsOnRangeAndConsent_NotOnChannelOrGrouping()
    {
        var range = query.Range;
        await service.GetReport(query, refresh: false, CancellationToken.None);
        await service.GetReport(query with { Range = range with { ChannelId = 2 } }, refresh: false, CancellationToken.None);
        await service.GetReport(query with { Range = range with { Grouping = StatsGrouping.Month } }, refresh: false, CancellationToken.None);
        Assert.That(repository.Calls, Is.EqualTo(1));

        await service.GetReport(query with { ConsentId = 1 }, refresh: false, CancellationToken.None);
        Assert.That(repository.Calls, Is.EqualTo(2));

        await service.GetReport(query with { Range = range with { From = new(2026, 9, 2) } }, refresh: false, CancellationToken.None);
        Assert.That(repository.Calls, Is.EqualTo(3));
    }

    [Test]
    public async Task GetReport_CachesWithExpiryOnly()
    {
        await service.GetReport(query, refresh: false, CancellationToken.None);

        var settings = cache.Settings.Single();
        Assert.That(settings.GetCacheDependency, Is.Null);
        Assert.That(settings.CacheMinutes, Is.EqualTo(StatsCache.CacheMinutes));
        Assert.That(settings.CacheItemName, Does.Contain("consents|"));
    }

    [Test]
    public void Filter_Normalize_DropsChannel_AndNonPositiveConsent()
    {
        var normalized = new ConsentsFilter { Range = new StatsFilter { ChannelId = 3 }, ConsentId = 0 }.Normalize(new(2026, 9, 30));

        Assert.That(normalized.Range.ChannelId, Is.Null);
        Assert.That(normalized.ConsentId, Is.Null);
        Assert.That(new ConsentsFilter { ConsentId = 4 }.Normalize(new(2026, 9, 30)).ConsentId, Is.EqualTo(4));
    }

    private sealed class FakeRepository : IConsentsRepository
    {
        public ConsentsReportData Data { get; set; } = ConsentsReportData.Empty;

        public int Calls { get; private set; }

        public (DateOnly PreviousFrom, DateOnly From, DateOnly To, int? ConsentId)? LastCall { get; private set; }

        public Task<ConsentsReportData> GetData(DateOnly previousFrom, DateOnly from, DateOnly to, int? consentId, CancellationToken cancellationToken)
        {
            Calls++;
            LastCall = (previousFrom, from, to, consentId);
            return Task.FromResult(Data);
        }
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

            var values = new List<object>();
            if (parameters?.TryGetValue(typeof(DataProtectionContentLanguage), out object? language) == true)
            {
                values.Add(language);
            }

            if (parameters?.TryGetValue(typeof(ConsentEditSection), out object? consent) == true)
            {
                values.Add(consent);
            }

            return string.Join('/', new object[] { string.Empty, typeof(TPage).Name }.Concat(values));
        }
    }
}
