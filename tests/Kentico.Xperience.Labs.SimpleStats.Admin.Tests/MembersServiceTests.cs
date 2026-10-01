using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Admin.Base.UIPages;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.Members;
using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Tests;

public class MembersServiceTests
{
    private static readonly StatsQuery range = new(new(2026, 9, 1), new(2026, 9, 30), StatsGrouping.Day, null);

    private FakeRepository repository = null!;
    private FakeCache cache = null!;
    private FakeClock clock = null!;
    private FakeAdminLinks adminLinks = null!;
    private MembersService service = null!;

    [SetUp]
    public void SetUp()
    {
        repository = new FakeRepository();
        cache = new FakeCache();
        clock = new FakeClock(new DateTimeOffset(2026, 9, 30, 10, 0, 0, TimeSpan.Zero));
        adminLinks = new FakeAdminLinks();
        service = new MembersService(repository, cache, cache, adminLinks, clock);
    }

    [Test]
    public async Task GetReport_ReadsPreviousPeriodAndRangeInOneCall()
    {
        await service.GetReport(range, refresh: false, CancellationToken.None);

        Assert.That(repository.Calls, Is.EqualTo(1));
        Assert.That(repository.LastCall, Is.EqualTo((new DateOnly(2026, 8, 2), range.From, range.To, MembersReportBuilder.RoleLimit)));
    }

    [Test]
    public async Task GetReport_TablesMissing_EmptyReport()
    {
        repository.Data = MembersReportData.Unavailable;

        var result = await service.GetReport(range, refresh: false, CancellationToken.None);

        Assert.That(result.Available, Is.False);
        Assert.That(result.Totals.NewMembers.Current, Is.Zero);
        Assert.That(result.ByRole.Items, Is.Empty);
    }

    [Test]
    public async Task GetReport_LinksMembersListing_AndEachRole()
    {
        repository.Data = MembersReportData.Empty with
        {
            Totals = MembersTotalsRow.Empty with { AllMembers = 3 },
            Roles = [new(5, "Premium", 3, 1)],
            RoleCount = 1,
        };

        var result = await service.GetReport(range, refresh: false, CancellationToken.None);

        Assert.That(result.MembersPath, Is.EqualTo("/MemberList"));
        Assert.That(result.ByRole.Items.Single().AdminPath, Is.EqualTo("/MemberRoleEdit/5"));
        Assert.That(adminLinks.Parameters.Single(), Is.EqualTo((typeof(MemberRoleEditSection), (object)5)));
    }

    [Test]
    public async Task GetReport_WorksWithoutLinks()
    {
        adminLinks.ReturnNull = true;
        repository.Data = MembersReportData.Empty with { Roles = [new(5, "Premium", 3, 1)], RoleCount = 1 };

        var result = await service.GetReport(range, refresh: false, CancellationToken.None);

        Assert.That(result.MembersPath, Is.Null);
        Assert.That(result.ByRole.Items.Single().AdminPath, Is.Null);
    }

    [Test]
    public async Task GetReport_UsesCachedData_WhenNotRefreshing_RefreshReadsAgain()
    {
        var first = await service.GetReport(range, refresh: false, CancellationToken.None);

        repository.Data = MembersReportData.Empty with { Daily = [new(new(2026, 9, 2), 2, 1)] };
        clock.Now = clock.Now.AddMinutes(1);
        var cached = await service.GetReport(range, refresh: false, CancellationToken.None);

        Assert.That(repository.Calls, Is.EqualTo(1));
        Assert.That(cached.Totals.NewMembers.Current, Is.Zero);
        Assert.That(cached.UpdatedAt, Is.EqualTo(first.UpdatedAt));

        var refreshed = await service.GetReport(range, refresh: true, CancellationToken.None);

        Assert.That(repository.Calls, Is.EqualTo(2));
        Assert.That(refreshed.Totals.NewMembers.Current, Is.EqualTo(3m));
        Assert.That(refreshed.UpdatedAt, Is.EqualTo(clock.Now));
        Assert.That(refreshed.ByRole.UpdatedAt, Is.EqualTo(clock.Now));
    }

    [Test]
    public async Task GetReport_CacheKey_SplitsOnRange_NotOnChannelOrGrouping()
    {
        await service.GetReport(range, refresh: false, CancellationToken.None);
        await service.GetReport(range with { ChannelId = 2 }, refresh: false, CancellationToken.None);
        await service.GetReport(range with { Grouping = StatsGrouping.Month }, refresh: false, CancellationToken.None);
        Assert.That(repository.Calls, Is.EqualTo(1));

        await service.GetReport(range with { From = new(2026, 9, 2) }, refresh: false, CancellationToken.None);
        Assert.That(repository.Calls, Is.EqualTo(2));
    }

    [Test]
    public async Task GetReport_CachesWithExpiryOnly()
    {
        await service.GetReport(range, refresh: false, CancellationToken.None);

        var settings = cache.Settings.Single();
        Assert.That(settings.GetCacheDependency, Is.Null);
        Assert.That(settings.CacheMinutes, Is.EqualTo(StatsCache.CacheMinutes));
        Assert.That(settings.CacheItemName, Does.Contain("members|"));
    }

    private sealed class FakeRepository : IMembersRepository
    {
        public MembersReportData Data { get; set; } = MembersReportData.Empty;

        public int Calls { get; private set; }

        public (DateOnly PreviousFrom, DateOnly From, DateOnly To, int Limit)? LastCall { get; private set; }

        public Task<MembersReportData> GetData(DateOnly previousFrom, DateOnly from, DateOnly to, int limit, CancellationToken cancellationToken)
        {
            Calls++;
            LastCall = (previousFrom, from, to, limit);
            return Task.FromResult(Data);
        }
    }

    private sealed class FakeAdminLinks : IStatsAdminLinks
    {
        public bool ReturnNull { get; set; }

        public List<(Type Page, object Value)> Parameters { get; } = [];

        public string? GetPath<TPage>(PageParameterValues? parameters = null)
        {
            if (ReturnNull)
            {
                return null;
            }

            object? value = null;
            if (parameters?.TryGetValue(typeof(MemberRoleEditSection), out value) == true)
            {
                Parameters.Add((typeof(MemberRoleEditSection), value));
            }

            return value is null ? $"/{typeof(TPage).Name}" : $"/{typeof(TPage).Name}/{value}";
        }
    }
}
