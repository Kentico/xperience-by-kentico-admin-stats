using Kentico.Xperience.AdminStats.Reports.Consents;
using Kentico.Xperience.AdminStats.Shared;

namespace Kentico.Xperience.AdminStats.Tests;

public class ConsentsReportBuilderTests
{
    // Previous period: Aug 2 – Aug 31.
    private static readonly ConsentsQuery query = new(new(new(2026, 9, 1), new(2026, 9, 30), StatsGrouping.Day, null), null);

    private static readonly ConsentsConsentRow tracking = new(1, "Tracking", 10, 5, 2, 40, 30, 4);

    private static readonly ConsentsConsentRow newsletter = new(2, "Newsletter", 3, 0, 1, 12, 10, 0);

    [Test]
    public void Build_NoConsents_ZerosAndNullRate()
    {
        var result = ConsentsReportBuilder.Build(query, ConsentsReportData.Empty);

        Assert.Multiple(() =>
        {
            Assert.That(result.Available, Is.True);
            Assert.That(result.Periods, Has.Count.EqualTo(30));
            Assert.That(result.Consents, Is.Empty);
            Assert.That(result.Agreements.Values, Is.All.Zero);
            Assert.That(result.AgreedContacts.Values, Is.All.Zero);
            Assert.That(result.Totals.Agreements.Current, Is.Zero);
            Assert.That(result.Totals.RevocationRate.Current, Is.Null);
            Assert.That(result.Totals.RevocationRate.Change, Is.Null);
            Assert.That(result.ByConsent.Items, Is.Empty);
            Assert.That(result.TextVersions, Is.Empty);
        });
    }

    [Test]
    public void Build_Unavailable_IsEmptyAndFlagged()
    {
        var result = ConsentsReportBuilder.Build(query, ConsentsReportData.Unavailable);

        Assert.That(result.Available, Is.False);
        Assert.That(result.AgreedContacts.Total, Is.Zero);
    }

    [Test]
    public void Build_ConsentsWithoutAgreements_ListedWithZero()
    {
        var data = ConsentsReportData.Empty with { Consents = [new(3, "Unused", 0, 0, 0, 0, 0, 0)] };

        var result = ConsentsReportBuilder.Build(query, data);

        Assert.Multiple(() =>
        {
            Assert.That(result.Consents.Single(), Is.EqualTo(new ConsentOption(3, "Unused")));
            Assert.That(result.ByConsent.Items.Single().Value, Is.Zero);
            Assert.That(result.TextVersions, Is.Empty);
            Assert.That(result.Totals.RevocationRate.Current, Is.Null);
        });
    }

    [Test]
    public void Build_EventsAndRate_RangeOnly_ChangeInPoints()
    {
        var data = ConsentsReportData.Empty with
        {
            Consents = [tracking],
            Daily = [new(new(2026, 8, 20), 10, 1, 9), new(new(2026, 9, 2), 6, 3, 3), new(new(2026, 9, 3), 4, 0, 4)],
        };

        var result = ConsentsReportBuilder.Build(query, data);

        Assert.Multiple(() =>
        {
            Assert.That(result.Agreements.Total, Is.EqualTo(10m));
            Assert.That(result.Revocations.Total, Is.EqualTo(3m));
            Assert.That(result.Agreements.Values[1], Is.EqualTo(6m));
            Assert.That(result.Totals.Agreements.Previous, Is.EqualTo(10m));
            Assert.That(result.Totals.Revocations.Previous, Is.EqualTo(1m));
            Assert.That(result.Totals.RevocationRate.Current, Is.EqualTo(0.3m));
            Assert.That(result.Totals.RevocationRate.Previous, Is.EqualTo(0.1m));
            Assert.That(result.Totals.RevocationRate.Change, Is.EqualTo(0.2).Within(1e-9));
        });
    }

    [Test]
    public void Build_AgreedContacts_PointInTimeFromStartAndChanges()
    {
        var data = ConsentsReportData.Empty with
        {
            Consents = [tracking],
            AgreedBeforeRange = 20,
            // Changes before the range are already in AgreedBeforeRange.
            Daily = [new(new(2026, 8, 20), 5, 0, 5), new(new(2026, 9, 2), 3, 1, 2), new(new(2026, 9, 15), 0, 1, -1)],
        };

        var result = ConsentsReportBuilder.Build(query with { Range = query.Range with { Grouping = StatsGrouping.Week } }, data);

        Assert.Multiple(() =>
        {
            Assert.That(result.AgreedContacts.Values[0], Is.EqualTo(22m));
            Assert.That(result.AgreedContacts.Values[^1], Is.EqualTo(21m));
            Assert.That(result.AgreedContacts.Total, Is.EqualTo(21m));
            Assert.That(result.Totals.AgreedContacts.Current, Is.EqualTo(21m));
            Assert.That(result.Totals.AgreedContacts.Previous, Is.EqualTo(20m));
        });
    }

    [Test]
    public void Build_AgreedContacts_NeverNegative()
    {
        var data = ConsentsReportData.Empty with { Daily = [new(new(2026, 9, 2), 0, 1, -1)] };

        var result = ConsentsReportBuilder.Build(query, data);

        Assert.That(result.AgreedContacts.Values, Is.All.GreaterThanOrEqualTo(0m));
    }

    [Test]
    public void Build_KnownConsent_IsApplied_UnknownMeansAll()
    {
        var data = ConsentsReportData.Empty with { Consents = [tracking, newsletter] };

        var known = ConsentsReportBuilder.Build(query with { ConsentId = 2 }, data);
        var unknown = ConsentsReportBuilder.Build(query with { ConsentId = 99 }, data);

        Assert.Multiple(() =>
        {
            Assert.That(known.ConsentId, Is.EqualTo(2));
            Assert.That(known.TextVersions.Select(t => t.Key), Is.EqualTo(new[] { "consent:2" }));
            // The consents list ignores the filter.
            Assert.That(known.ByConsent.Items, Has.Count.EqualTo(2));
            Assert.That(unknown.ConsentId, Is.Null);
            Assert.That(unknown.TextVersions, Has.Count.EqualTo(2));
        });
    }

    [Test]
    public void Build_ByConsent_AgreedNowWithPreviousAndEvents_SharesOfDistinctContacts()
    {
        var data = ConsentsReportData.Empty with { Consents = [newsletter, tracking], AllAgreedContacts = 45 };

        var result = ConsentsReportBuilder.Build(query, data, id => $"/agreements/{id}");
        var first = result.ByConsent.Items[0];

        Assert.Multiple(() =>
        {
            Assert.That(result.ByConsent.Items.Select(i => i.Label), Is.EqualTo(new[] { "Tracking", "Newsletter" }));
            Assert.That(first.Value, Is.EqualTo(40m));
            Assert.That(first.PreviousValue, Is.EqualTo(30m));
            Assert.That(first.Change, Is.EqualTo(1d / 3).Within(1e-9));
            Assert.That(first.SecondaryValue, Is.EqualTo(10m));
            Assert.That(first.TertiaryValue, Is.EqualTo(2m));
            Assert.That(first.AdminPath, Is.EqualTo("/agreements/1"));
            // A contact can agree to several consents: the total is distinct contacts, shares do not add up to 100%.
            Assert.That(result.ByConsent.Total, Is.EqualTo(45m));
            Assert.That(first.Share, Is.EqualTo(40d / 45).Within(1e-9));
            Assert.That(result.ByConsent.From, Is.EqualTo(query.Range.From));
        });
    }

    [Test]
    public void Build_TextVersions_CurrentOfAgreed()
    {
        var data = ConsentsReportData.Empty with { Consents = [tracking, newsletter] };

        var result = ConsentsReportBuilder.Build(query, data);
        var first = result.TextVersions[0];

        Assert.Multiple(() =>
        {
            Assert.That(first.Label, Is.EqualTo("Tracking"));
            Assert.That(first.Covered, Is.EqualTo(36));
            Assert.That(first.Total, Is.EqualTo(40));
            Assert.That(first.Missing, Is.EqualTo(4));
            Assert.That(result.TextVersions[1].Missing, Is.Zero);
        });
    }

    [Test]
    public void Build_ConsentWithoutName_GetsIdLabel()
    {
        var data = ConsentsReportData.Empty with { Consents = [new(7, " ", 1, 0, 0, 1, 0, 0)] };

        var result = ConsentsReportBuilder.Build(query, data);

        Assert.That(result.Consents.Single().DisplayName, Is.EqualTo("Consent #7"));
    }
}
