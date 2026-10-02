using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Admin.DigitalMarketing.UIPages;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.EmailSummary;
using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;
using Kentico.Xperience.Labs.SimpleStats.Admin.UIPages;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Tests;

public class EmailSummaryServiceTests
{
    private static readonly StatsQuery query = new(new(2026, 9, 1), new(2026, 9, 30), StatsGrouping.Day, null);

    private static readonly EmailSummaryReportData data = EmailSummaryReportData.Empty with
    {
        Channels = [new(1, 7, "en")],
        Emails = [new(3, "Newsletter", "Newsletter #3", 1, "en", new(2026, 9, 10, 9, 0, 0, DateTimeKind.Unspecified), "List", new(100, 100, 50, 10, null, 0, 1, null))],
        Automated = [new(4, "Welcome", "Welcome", "Automation", 1, "es", 2, new(10, 10, 5, 1, null, 0, 0, null))],
    };

    private FakeRepository repository = null!;
    private FakeCache cache = null!;
    private FakeClock clock = null!;
    private FakeAdminLinks adminLinks = null!;
    private EmailSummaryService service = null!;

    [SetUp]
    public void SetUp()
    {
        repository = new FakeRepository();
        cache = new FakeCache();
        clock = new FakeClock(new DateTimeOffset(2026, 9, 30, 10, 0, 0, TimeSpan.Zero));
        adminLinks = new FakeAdminLinks();
        service = new EmailSummaryService(repository, cache, cache, adminLinks, clock);
    }

    [Test]
    public async Task GetReport_ReadsPreviousPeriodAndRangeInOneCall()
    {
        await service.GetReport(query with { ChannelId = 7, Grouping = StatsGrouping.Week }, refresh: false, CancellationToken.None);

        Assert.That(repository.Calls, Is.EqualTo(1));
        Assert.That(repository.LastCall, Is.EqualTo((new DateOnly(2026, 8, 2), query.From, query.To, (int?)7, StatsGrouping.Week)));
    }

    [Test]
    public async Task GetReport_TablesMissing_EmptyReport()
    {
        repository.Data = EmailSummaryReportData.Unavailable;

        var result = await service.GetReport(query, refresh: false, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Available, Is.False);
            Assert.That(result.Emails, Is.Empty);
            Assert.That(result.EmailsAppPath, Is.Null);
        });
    }

    [Test]
    public async Task GetReport_LinksStatisticsTabs_AndEmailList()
    {
        repository.Data = data;

        var result = await service.GetReport(query, refresh: false, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Emails.Single().StatisticsPath, Is.EqualTo("/emails-1/en/list/3/statistics"));
            Assert.That(result.Automated.Single().StatisticsPath, Is.EqualTo("/emails-1/es/list/4/statistics"));
            Assert.That(result.ByOpenRate.Top.Items.Single().AdminPath, Is.EqualTo("/emails-1/en/list/3/statistics"));
            Assert.That(result.EmailsAppPath, Is.EqualTo("/emails-1/en/list"));
        });
    }

    [Test]
    public async Task GetReport_WorksWithoutLinks()
    {
        repository.Data = data;
        adminLinks.ReturnNull = true;

        var result = await service.GetReport(query, refresh: false, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Emails.Single().StatisticsPath, Is.Null);
            Assert.That(result.EmailsAppPath, Is.Null);
        });
    }

    [Test]
    public void ChannelParameters_MatchProductHelper()
    {
        var parameters = EmailSummaryService.GetChannelParameters(12, "en-US");

        Assert.Multiple(() =>
        {
            Assert.That(parameters[typeof(EmailChannelApplication)], Is.EqualTo("emails-12"));
            Assert.That(parameters[typeof(EmailChannelContentLanguage)], Is.EqualTo("en-US"));
        });
    }

    [Test]
    public async Task GetReport_UsesCachedData_WhenNotRefreshing_RefreshReadsAgain()
    {
        var first = await service.GetReport(query, refresh: false, CancellationToken.None);

        repository.Data = data;
        clock.Now = clock.Now.AddMinutes(1);
        var cached = await service.GetReport(query, refresh: false, CancellationToken.None);

        Assert.That(repository.Calls, Is.EqualTo(1));
        Assert.That(cached.Emails, Is.Empty);
        Assert.That(cached.UpdatedAt, Is.EqualTo(first.UpdatedAt));

        var refreshed = await service.GetReport(query, refresh: true, CancellationToken.None);

        Assert.That(repository.Calls, Is.EqualTo(2));
        Assert.That(refreshed.Emails, Has.Count.EqualTo(1));
        Assert.That(refreshed.UpdatedAt, Is.EqualTo(clock.Now));
    }

    [Test]
    public async Task GetReport_CacheKey_SplitsOnRangeChannelAndGrouping()
    {
        await service.GetReport(query, refresh: false, CancellationToken.None);
        await service.GetReport(query, refresh: false, CancellationToken.None);
        Assert.That(repository.Calls, Is.EqualTo(1));

        await service.GetReport(query with { ChannelId = 7 }, refresh: false, CancellationToken.None);
        Assert.That(repository.Calls, Is.EqualTo(2));

        await service.GetReport(query with { Grouping = StatsGrouping.Month }, refresh: false, CancellationToken.None);
        Assert.That(repository.Calls, Is.EqualTo(3));

        await service.GetReport(query with { From = new(2026, 9, 2) }, refresh: false, CancellationToken.None);
        Assert.That(repository.Calls, Is.EqualTo(4));
    }

    [Test]
    public async Task GetReport_CachesWithExpiryOnly()
    {
        await service.GetReport(query, refresh: false, CancellationToken.None);

        var settings = cache.Settings.Single();
        Assert.That(settings.GetCacheDependency, Is.Null);
        Assert.That(settings.CacheMinutes, Is.EqualTo(StatsCache.CacheMinutes));
        Assert.That(settings.CacheItemName, Does.Contain("email-summary|"));
    }

    [Test]
    public void Page_NormalizeChannel_KeepsEmailChannelsOnly()
    {
        IReadOnlyList<StatsChannelOption> channels = [new(7, "Emails", "Email")];

        Assert.Multiple(() =>
        {
            Assert.That(EmailSummaryPage.NormalizeChannel(query with { ChannelId = 7 }, channels).ChannelId, Is.EqualTo(7));
            Assert.That(EmailSummaryPage.NormalizeChannel(query with { ChannelId = 2 }, channels).ChannelId, Is.Null);
            Assert.That(EmailSummaryPage.NormalizeChannel(query, channels).ChannelId, Is.Null);
        });
    }

    private sealed class FakeRepository : IEmailSummaryRepository
    {
        public EmailSummaryReportData Data { get; set; } = EmailSummaryReportData.Empty;

        public int Calls { get; private set; }

        public (DateOnly PreviousFrom, DateOnly From, DateOnly To, int? ChannelId, StatsGrouping Grouping)? LastCall { get; private set; }

        public Task<EmailSummaryReportData> GetData(
            DateOnly previousFrom,
            DateOnly from,
            DateOnly to,
            int? channelId,
            StatsGrouping grouping,
            CancellationToken cancellationToken)
        {
            Calls++;
            LastCall = (previousFrom, from, to, channelId, grouping);
            return Task.FromResult(Data);
        }
    }

    /// <summary>
    /// Builds paths like the product's email pages: <c>/{application}/{language}/list[/{email}/statistics]</c>.
    /// </summary>
    private sealed class FakeAdminLinks : IStatsAdminLinks
    {
        public bool ReturnNull { get; set; }

        public string? GetPath<TPage>(PageParameterValues? parameters = null)
        {
            if (ReturnNull || parameters is null)
            {
                return null;
            }

            string path = $"/{parameters[typeof(EmailChannelApplication)]}/{parameters[typeof(EmailChannelContentLanguage)]}/list";

            if (typeof(TPage) == typeof(EmailStatisticsTab))
            {
                return $"{path}/{parameters[typeof(EmailEditLayout)]}/statistics";
            }

            return typeof(TPage) == typeof(EmailList) ? path : null;
        }
    }
}
