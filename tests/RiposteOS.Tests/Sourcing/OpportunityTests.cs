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

        Assert.True(opportunity.RefreshFromSource(
            "  Nouveau titre  ",
            "  Nouvel acheteur  ",
            new DateOnly(2026, 7, 16),
            null,
            ["FRA"],
            [" 69 ", "69"],
            [" 72200000 "],
            ["186"],
            [" Logiciel "],
            75,
            [" +15 Signal positif : logiciel "],
            "  https://example.test/notice  ",
            " {\"updated\":true} ",
            ImportedAt.AddHours(1),
            "  Description détaillée  ",
            "  open  ",
            "  services  ",
            250_000m,
            " EUR ",
            " 24 MONTH ",
            " https://example.test/dce "));

        Assert.Equal("BOAMP", opportunity.Source);
        Assert.Equal("source-id", opportunity.SourceId);
        Assert.Equal("Nouveau titre", opportunity.Title);
        Assert.Equal("Nouvel acheteur", opportunity.Buyer);
        Assert.Equal(["FRA"], opportunity.CountryCodes);
        Assert.Equal(["69"], opportunity.DepartmentCodes);
        Assert.Equal(["72200000"], opportunity.CpvCodes);
        Assert.Equal(["186"], opportunity.DescriptorCodes);
        Assert.Equal(["Logiciel"], opportunity.DescriptorLabels);
        Assert.IsNotType<string[]>(opportunity.DepartmentCodes);
        Assert.Equal(75, opportunity.MatchScore);
        Assert.Equal(["+15 Signal positif : logiciel"], opportunity.MatchReasons);
        Assert.Equal("Description détaillée", opportunity.Description);
        Assert.Equal("open", opportunity.ProcedureType);
        Assert.Equal("services", opportunity.ContractNature);
        Assert.Equal(250_000m, opportunity.EstimatedValue);
        Assert.Equal("EUR", opportunity.Currency);
        Assert.Equal("24 MONTH", opportunity.ExecutionDuration);
        Assert.Equal("https://example.test/dce", opportunity.DocumentUrl);
        Assert.Equal("https://example.test/notice", opportunity.NoticeUrl);
        Assert.Equal("{\"updated\":true}", opportunity.RawPayload);
    }

    [Fact]
    public void EquivalentNormalizedContentDoesNotRefreshTheOpportunity()
    {
        var opportunity = CreateOpportunity(
            "{\"nested\":{\"b\":2,\"a\":1},\"id\":\"source-id\"}");
        var initialHash = opportunity.ContentHash;

        var changed = opportunity.RefreshFromSource(
            " Title ",
            " Buyer ",
            new DateOnly(2026, 7, 15),
            ImportedAt.AddDays(10),
            ["FRA"],
            ["69"],
            ["72200000"],
            ["186"],
            ["Logiciel"],
            40,
            ["+25 CPV ciblé : 72200000"],
            "",
            " { \"id\": \"source-id\", \"nested\": { \"a\": 1, \"b\": 2 } } ",
            ImportedAt.AddHours(1));

        Assert.False(changed);
        Assert.Equal(initialHash, opportunity.ContentHash);
        Assert.Equal(ImportedAt, opportunity.UpdatedAt);
    }

    [Fact]
    public void CanonicalPayloadSupportsEveryJsonValueKind()
    {
        var opportunity = CreateOpportunity("[true,false,null,1,1.5,\"value\"]");

        var changed = opportunity.RefreshFromSource(
            "Title",
            "Buyer",
            new DateOnly(2026, 7, 15),
            ImportedAt.AddDays(10),
            ["FRA"],
            ["69"],
            ["72200000"],
            ["186"],
            ["Logiciel"],
            40,
            ["+25 CPV ciblé : 72200000"],
            "",
            " [ true, false, null, 1, 1.5, \"value\" ] ",
            ImportedAt.AddHours(1));

        Assert.False(changed);
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
            ["FRA"],
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
    public void RevisionRequiresAnOpportunityAndCannotPredateItsImport()
    {
        var opportunity = CreateOpportunity();

        Assert.Throws<ArgumentNullException>(() =>
            new OpportunityRevision(null!, ImportedAt));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new OpportunityRevision(opportunity, ImportedAt.AddTicks(-1)));
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
    public void EformsNoticeIdentityCanOnlyBeAssignedOnce()
    {
        var opportunity = CreateOpportunity();
        var noticeId = Guid.NewGuid();

        Assert.False(opportunity.IdentifyByEformsNotice(null));
        Assert.True(opportunity.IdentifyByEformsNotice(noticeId));
        Assert.False(opportunity.IdentifyByEformsNotice(noticeId));
        Assert.Throws<InvalidOperationException>(() =>
            opportunity.IdentifyByEformsNotice(Guid.NewGuid()));
    }

    [Fact]
    public void PublicationsAndRevisionsCanBeReassignedDuringCanonicalMerge()
    {
        var duplicate = CreateOpportunity();
        var canonical = CreateOpportunity();
        var publication = duplicate.AddPublication(
            SourcingSource.Boamp,
            "26-1",
            string.Empty,
            null,
            "{}",
            ImportedAt);
        var revision = new OpportunityRevision(duplicate, ImportedAt);

        publication.ReassignTo(canonical);
        revision.ReassignTo(canonical);

        Assert.Same(canonical, publication.Opportunity);
        Assert.Same(canonical, revision.Opportunity);
        Assert.Throws<ArgumentNullException>(() => publication.ReassignTo(null!));
        Assert.Throws<ArgumentNullException>(() => revision.ReassignTo(null!));
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
            [],
            0,
            [],
            "",
            rawPayload,
            ImportedAt));
    }

    [Fact]
    public void EstimatedValueCannotBeNegative()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Opportunity(
            "BOAMP",
            "source-id",
            "Title",
            "Buyer",
            new DateOnly(2026, 7, 15),
            null,
            [],
            [],
            [],
            [],
            [],
            0,
            [],
            "",
            "{}",
            ImportedAt,
            estimatedValue: -1));
    }

    private static Opportunity CreateOpportunity(string rawPayload = "{}") => new(
        " boamp ",
        " source-id ",
        " Title ",
        " Buyer ",
        new DateOnly(2026, 7, 15),
        ImportedAt.AddDays(10),
        ["FRA"],
        ["69"],
        ["72200000"],
        ["186"],
        ["Logiciel"],
        40,
        ["+25 CPV ciblé : 72200000"],
        "",
        rawPayload,
        ImportedAt);
}
