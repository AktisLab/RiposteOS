using RiposteOS.Core.Sourcing;

namespace RiposteOS.Tests.Sourcing;

public sealed class OpportunityPublicationTests
{
    private static readonly DateTimeOffset FirstSeenAt =
        new(2026, 7, 17, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void PublicationIsTraceableAndOnlyRefreshesOnChange()
    {
        var opportunity = CreateOpportunity();
        var publication = opportunity.AddPublication(
            " boamp ",
            " 26-1 ",
            " https://example.test/notice ",
            " https://example.test/dce ",
            " {\"version\":1} ",
            FirstSeenAt);
        var initialHash = publication.ContentHash;

        Assert.Equal("BOAMP", publication.Source);
        Assert.Equal("26-1", publication.SourceId);
        Assert.Equal("https://example.test/notice", publication.NoticeUrl);
        Assert.Equal("https://example.test/dce", publication.DocumentUrl);
        Assert.False(publication.Refresh(
            publication.NoticeUrl,
            publication.DocumentUrl,
            publication.RawPayload,
            FirstSeenAt.AddHours(1)));
        Assert.Equal(FirstSeenAt, publication.UpdatedAt);

        Assert.True(publication.Refresh(
            publication.NoticeUrl,
            null,
            "{\"version\":2}",
            FirstSeenAt.AddHours(2)));
        Assert.NotEqual(initialHash, publication.ContentHash);
        Assert.Equal(string.Empty, publication.DocumentUrl);
        Assert.Equal(FirstSeenAt.AddHours(2), publication.UpdatedAt);
    }

    [Fact]
    public void PublicationGuardsIdentityDatesAndDuplicates()
    {
        var opportunity = CreateOpportunity();
        var publication = opportunity.AddPublication(
            SourcingSource.Boamp,
            "26-1",
            string.Empty,
            null,
            "{}",
            FirstSeenAt);

        Assert.Throws<InvalidOperationException>(() => opportunity.AddPublication(
            "boamp",
            "26-1",
            string.Empty,
            null,
            "{}",
            FirstSeenAt));
        Assert.Throws<ArgumentOutOfRangeException>(() => publication.Refresh(
            string.Empty,
            null,
            "{}",
            FirstSeenAt.AddTicks(-1)));
        Assert.Throws<ArgumentException>(() => CreateOpportunity().AddPublication(
            SourcingSource.Boamp,
            " ",
            string.Empty,
            null,
            "{}",
            FirstSeenAt));
        Assert.Throws<ArgumentException>(() => CreateOpportunity().AddPublication(
            SourcingSource.Boamp,
            "26-2",
            string.Empty,
            null,
            " ",
            FirstSeenAt));
    }

    private static Opportunity CreateOpportunity() => new(
        SourcingSource.Boamp,
        "26-1",
        "Logiciel métier",
        "Acheteur",
        new DateOnly(2026, 7, 17),
        null,
        ["FRA"],
        ["69"],
        ["72200000"],
        [],
        [],
        40,
        [],
        string.Empty,
        "{}",
        FirstSeenAt);
}
