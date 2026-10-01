using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.Members;
using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Tests;

public class MembersReportBuilderTests
{
    // Previous period: Aug 2 – Aug 31.
    private static readonly StatsQuery range = new(new(2026, 9, 1), new(2026, 9, 30), StatsGrouping.Day, null);

    [Test]
    public void Build_NoMembers_ZerosAndNullShare()
    {
        var result = MembersReportBuilder.Build(range, MembersReportData.Empty);

        Assert.Multiple(() =>
        {
            Assert.That(result.Available, Is.True);
            Assert.That(result.Periods, Has.Count.EqualTo(30));
            Assert.That(result.NewMembers.Values, Is.All.Zero);
            Assert.That(result.TotalMembers.Values, Is.All.Zero);
            Assert.That(result.Totals.NewMembers.Current, Is.Zero);
            Assert.That(result.Totals.NewMembers.Change, Is.Null);
            Assert.That(result.Totals.ExternalShare.Current, Is.Null);
            Assert.That(result.Totals.ExternalShare.Change, Is.Null);
            Assert.That(result.Totals.DisabledMembers, Is.Zero);
            Assert.That(result.ByRole.Items, Is.Empty);
        });
    }

    [Test]
    public void Build_Unavailable_IsEmptyAndFlagged()
    {
        var result = MembersReportBuilder.Build(range, MembersReportData.Unavailable);

        Assert.That(result.Available, Is.False);
        Assert.That(result.TotalMembers.Total, Is.Zero);
    }

    [Test]
    public void Build_SplitsInternalAndExternal_RangeOnly()
    {
        var data = MembersReportData.Empty with
        {
            Daily = [new(new(2026, 8, 20), 5, 5), new(new(2026, 9, 2), 3, 1), new(new(2026, 9, 3), 0, 2)],
        };

        var result = MembersReportBuilder.Build(range, data);

        Assert.Multiple(() =>
        {
            Assert.That(result.NewMembers.Total, Is.EqualTo(6m));
            Assert.That(result.InternalMembers.Total, Is.EqualTo(3m));
            Assert.That(result.ExternalMembers.Total, Is.EqualTo(3m));
            Assert.That(result.NewMembers.Values[1], Is.EqualTo(4m));
            Assert.That(result.ExternalMembers.Values[2], Is.EqualTo(2m));
            Assert.That(result.Totals.NewMembers.Current, Is.EqualTo(6m));
            Assert.That(result.Totals.NewMembers.Previous, Is.EqualTo(10m));
            Assert.That(result.Totals.NewMembers.Change, Is.EqualTo(-0.4).Within(1e-9));
            Assert.That(result.Totals.ExternalShare.Current, Is.EqualTo(0.5m));
            Assert.That(result.Totals.ExternalShare.Previous, Is.EqualTo(0.5m));
            // Ratio change in percentage points.
            Assert.That(result.Totals.ExternalShare.Change, Is.EqualTo(0d));
        });
    }

    [Test]
    public void Build_AllExternal_ShareIsOne()
    {
        var data = MembersReportData.Empty with { Daily = [new(new(2026, 9, 5), 0, 4)] };

        var result = MembersReportBuilder.Build(range, data);

        Assert.That(result.Totals.ExternalShare.Current, Is.EqualTo(1m));
        // No new members in the previous period: no share, no change.
        Assert.That(result.Totals.ExternalShare.Previous, Is.Null);
        Assert.That(result.Totals.ExternalShare.Change, Is.Null);
        Assert.That(result.Totals.NewMembers.Change, Is.Null);
    }

    [Test]
    public void Build_TotalMembers_StartsWithMembersBeforeRange()
    {
        var data = MembersReportData.Empty with
        {
            Daily = [new(new(2026, 8, 20), 9, 0), new(new(2026, 9, 1), 2, 0), new(new(2026, 9, 10), 1, 1)],
            Totals = MembersTotalsRow.Empty with { MembersBeforeRange = 50, AllMembers = 54 },
        };

        var result = MembersReportBuilder.Build(range with { Grouping = StatsGrouping.Week }, data);

        Assert.Multiple(() =>
        {
            Assert.That(result.TotalMembers.Values[0], Is.EqualTo(52m));
            Assert.That(result.TotalMembers.Values[^1], Is.EqualTo(54m));
            Assert.That(result.TotalMembers.Total, Is.EqualTo(54m));
            // Point in time: range end vs previous period end.
            Assert.That(result.Totals.TotalMembers.Current, Is.EqualTo(54m));
            Assert.That(result.Totals.TotalMembers.Previous, Is.EqualTo(50m));
            Assert.That(result.Totals.TotalMembers.Change, Is.EqualTo(0.08).Within(1e-9));
        });
    }

    [Test]
    public void Build_DisabledMembers_IsCurrentSnapshot()
    {
        var data = MembersReportData.Empty with { Totals = MembersTotalsRow.Empty with { DisabledMembers = 7 } };

        Assert.That(MembersReportBuilder.Build(range, data).Totals.DisabledMembers, Is.EqualTo(7));
    }

    [Test]
    public void Build_Roles_WithNoRoleRow_SharesOfAllMembers_AndLinks()
    {
        var data = MembersReportData.Empty with
        {
            Totals = MembersTotalsRow.Empty with { AllMembers = 20, NoRoleMembers = 8, NoRoleNewMembers = 3 },
            Roles = [new(5, "Premium", 10, 4), new(6, "  ", 4, 0), new(7, "Empty", 0, 0)],
            RoleCount = 3,
        };

        var result = MembersReportBuilder.Build(range, data, id => $"/roles/{id}").ByRole;

        Assert.Multiple(() =>
        {
            Assert.That(result.Items.Select(i => i.Label), Is.EqualTo(new[] { "Premium", MembersReportBuilder.NoRoleLabel, "Role #6" }));
            Assert.That(result.Items.Select(i => i.SecondaryValue), Is.EqualTo(new decimal?[] { 4, 3, 0 }));
            Assert.That(result.Items[0].Share, Is.EqualTo(0.5));
            Assert.That(result.Items[1].Share, Is.EqualTo(0.4));
            Assert.That(result.Items[0].AdminPath, Is.EqualTo("/roles/5"));
            Assert.That(result.Items[1].AdminPath, Is.Null);
            Assert.That(result.Items[1].Key, Is.EqualTo(MembersReportBuilder.NoRoleKey));
            Assert.That(result.Total, Is.EqualTo(20m));
            Assert.That(result.ItemCount, Is.EqualTo(4));
        });
    }

    [Test]
    public void Build_AllMembersWithoutRoles_OnlyNoRoleRow()
    {
        var data = MembersReportData.Empty with { Totals = MembersTotalsRow.Empty with { AllMembers = 5, NoRoleMembers = 5 } };

        var items = MembersReportBuilder.Build(range, data).ByRole.Items;

        Assert.That(items.Select(i => (i.Label, i.Value, i.Share)), Is.EqualTo(new[] { (MembersReportBuilder.NoRoleLabel, 5m, 1d) }));
    }

    [Test]
    public void Build_DropsChannel() =>
        Assert.That(MembersReportBuilder.Build(range with { ChannelId = 3 }, MembersReportData.Empty).ByRole.ChannelId, Is.Null);
}
