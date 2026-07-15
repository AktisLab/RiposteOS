using RiposteOS.Core.Sourcing;

namespace RiposteOS.Tests.Sourcing;

public sealed class OpportunityTests
{
    private static readonly DateTimeOffset ImportedAt =
        new(2026, 7, 15, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CreationAndRefreshNormalizeValuesWithoutExposingMutableCollections()
    {
        var opportunity = CreateOpportunity();

        opportunity.RefreshFromSource(
            "  Nouveau titre  ",
            "  Nouvel acheteur  ",
            new DateOnly(2026, 7, 16),
            null,
            [" 69 ", "69"],
            [" 72200000 "],
            ["186"],
            [" Logiciel "],
            75,
            [" +15 Signal positif : logiciel "],
            "  https://example.test/notice  ",
            " {\"updated\":true} ",
            ImportedAt.AddHours(1));

        Assert.Equal("BOAMP", opportunity.Source);
        Assert.Equal("source-id", opportunity.SourceId);
        Assert.Equal("Nouveau titre", opportunity.Title);
        Assert.Equal("Nouvel acheteur", opportunity.Buyer);
        Assert.Equal(["69"], opportunity.DepartmentCodes);
        Assert.Equal(["72200000"], opportunity.CpvCodes);
        Assert.Equal(["186"], opportunity.DescriptorCodes);
        Assert.Equal(["Logiciel"], opportunity.DescriptorLabels);
        Assert.IsNotType<string[]>(opportunity.DepartmentCodes);
        Assert.Equal(75, opportunity.MatchScore);
        Assert.Equal(["+15 Signal positif : logiciel"], opportunity.MatchReasons);
        Assert.Equal("https://example.test/notice", opportunity.NoticeUrl);
        Assert.Equal("{\"updated\":true}", opportunity.RawPayload);
    }

    [Fact]
    public void RefreshCannotPredateInitialImport()
    {
        var opportunity = CreateOpportunity();

        Assert.Throws<ArgumentOutOfRangeException>(() => opportunity.RefreshFromSource(
            "Title",
            "Buyer",
            new DateOnly(2026, 7, 15),
            null,
            [],
            [],
            [],
            [],
            0,
            [],
            "",
            "{}",
            ImportedAt.AddTicks(-1)));
    }

    [Fact]
    public void QualificationTransitionsExpressTheirIntent()
    {
        var opportunity = CreateOpportunity();

        opportunity.Retain();
        Assert.Equal(OpportunityStatus.Retained, opportunity.Status);
        opportunity.Dismiss();
        Assert.Equal(OpportunityStatus.Dismissed, opportunity.Status);
        opportunity.ReturnToQualification();
        Assert.Equal(OpportunityStatus.ToQualify, opportunity.Status);
    }

    [Fact]
    public void MatchCanBeReassessedWithoutChangingSourceData()
    {
        var opportunity = CreateOpportunity();
        var reassessedAt = ImportedAt.AddHours(1);

        opportunity.ReassessMatch(
            0,
            ["-50 CPV exclu : 72267100"],
            reassessedAt);

        Assert.Equal(0, opportunity.MatchScore);
        Assert.Equal(["-50 CPV exclu : 72267100"], opportunity.MatchReasons);
        Assert.Equal("Title", opportunity.Title);
        Assert.Equal(reassessedAt, opportunity.UpdatedAt);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            opportunity.ReassessMatch(10, [], ImportedAt));
    }

    [Theory]
    [InlineData("", "source-id", "Title", "Buyer", "{}")]
    [InlineData("BOAMP", "", "Title", "Buyer", "{}")]
    [InlineData("BOAMP", "source-id", "", "Buyer", "{}")]
    [InlineData("BOAMP", "source-id", "Title", "", "{}")]
    [InlineData("BOAMP", "source-id", "Title", "Buyer", "")]
    public void RequiredCreationValuesAreGuarded(
        string source,
        string sourceId,
        string title,
        string buyer,
        string rawPayload)
    {
        Assert.Throws<ArgumentException>(() => new Opportunity(
            source,
            sourceId,
            title,
            buyer,
            new DateOnly(2026, 7, 15),
            null,
            [],
            [],
            [],
            [],
            0,
            [],
            "",
            rawPayload,
            ImportedAt));
    }

    private static Opportunity CreateOpportunity() => new(
        " boamp ",
        " source-id ",
        " Title ",
        " Buyer ",
        new DateOnly(2026, 7, 15),
        ImportedAt.AddDays(10),
        ["69"],
        ["72200000"],
        ["186"],
        ["Logiciel"],
        40,
        ["+25 CPV ciblé : 72200000"],
        "",
        "{}",
        ImportedAt);
}
