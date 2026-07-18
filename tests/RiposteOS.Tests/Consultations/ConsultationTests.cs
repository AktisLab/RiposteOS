using Microsoft.EntityFrameworkCore;
using RiposteOS.Core.Consultations;
using RiposteOS.Core.Sourcing;
using RiposteOS.Infrastructure.Persistence;

namespace RiposteOS.Tests.Consultations;

public sealed class ConsultationTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 7, 17, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ManualCreationNormalizesValuesAndStartsWithoutOpportunity()
    {
        var deadline = CreatedAt.AddDays(10);

        var consultation = new Consultation(
            "  Portail citoyen  ",
            "  Ville de Lyon  ",
            deadline,
            "  https://example.test/avis  ",
            CreatedAt);

        Assert.Null(consultation.OpportunityId);
        Assert.Equal("Portail citoyen", consultation.Title);
        Assert.Equal("Ville de Lyon", consultation.Buyer);
        Assert.Equal(deadline, consultation.ResponseDeadline);
        Assert.Equal("https://example.test/avis", consultation.NoticeUrl);
        Assert.Equal(CreatedAt, consultation.CreatedAt);
        Assert.Equal(CreatedAt, consultation.UpdatedAt);
    }

    [Fact]
    public async Task CreationFromOpportunityCopiesAnInitialSnapshot()
    {
        var opportunity = CreateOpportunity();
        await using var dbContext = CreateDbContext();
        dbContext.Add(opportunity);
        await dbContext.SaveChangesAsync();

        var consultation = Consultation.FromOpportunity(opportunity, CreatedAt.AddHours(1));

        Assert.Equal(opportunity.Id, consultation.OpportunityId);
        Assert.Equal(opportunity.Title, consultation.Title);
        Assert.Equal(opportunity.Buyer, consultation.Buyer);
        Assert.Equal(opportunity.ResponseDeadline, consultation.ResponseDeadline);
        Assert.Equal(opportunity.NoticeUrl, consultation.NoticeUrl);
        Assert.Equal(CreatedAt.AddHours(1), consultation.CreatedAt);
        Assert.Equal(consultation.CreatedAt, consultation.UpdatedAt);
    }

    [Theory]
    [InlineData(null, "Acheteur")]
    [InlineData("", "Acheteur")]
    [InlineData("Titre", null)]
    [InlineData("Titre", " ")]
    public void RequiredValuesAreRejected(string? title, string? buyer)
    {
        Assert.ThrowsAny<ArgumentException>(() => new Consultation(
            title!, buyer!, null, null, CreatedAt));
    }

    [Fact]
    public void OverlongValuesAndRelativeNoticeUrlAreRejected()
    {
        Assert.Throws<ArgumentException>(() => new Consultation(
            new string('a', Consultation.MaximumTitleLength + 1), "Acheteur", null, null, CreatedAt));
        Assert.Throws<ArgumentException>(() => new Consultation(
            "Titre", new string('a', Consultation.MaximumBuyerLength + 1), null, null, CreatedAt));
        Assert.Throws<ArgumentException>(() => new Consultation(
            "Titre", "Acheteur", null, new string('a', Consultation.MaximumNoticeUrlLength + 1), CreatedAt));
        Assert.Throws<ArgumentException>(() => new Consultation(
            "Titre", "Acheteur", null, "/avis/26-1", CreatedAt));
    }

    [Fact]
    public void BlankNoticeUrlIsNormalizedToNull()
    {
        var consultation = new Consultation("Titre", "Acheteur", null, "  ", CreatedAt);

        Assert.Null(consultation.NoticeUrl);
    }

    [Fact]
    public void CreationFromOpportunityRequiresAPersistedOpportunity()
    {
        Assert.Throws<ArgumentNullException>(() => Consultation.FromOpportunity(null!, CreatedAt));
        Assert.Throws<ArgumentException>(() => Consultation.FromOpportunity(CreateOpportunity(), CreatedAt));
    }

    [Fact]
    public void ReassignmentRequiresAnIdentifierAndMonotonicUpdateDate()
    {
        var consultation = new Consultation("Titre", "Acheteur", null, null, CreatedAt);
        var opportunityId = Guid.NewGuid();

        consultation.ReassignToOpportunity(opportunityId, CreatedAt.AddMinutes(1));

        Assert.Equal(opportunityId, consultation.OpportunityId);
        Assert.Equal(CreatedAt.AddMinutes(1), consultation.UpdatedAt);
        Assert.Throws<ArgumentException>(() => consultation.ReassignToOpportunity(Guid.Empty, CreatedAt.AddMinutes(2)));
        Assert.Throws<ArgumentOutOfRangeException>(() => consultation.ReassignToOpportunity(Guid.NewGuid(), CreatedAt));
    }

    private static Opportunity CreateOpportunity() => new(
        SourcingSource.Boamp,
        "26-1",
        "Logiciel métier",
        "Métropole de Lyon",
        new DateOnly(2026, 7, 17),
        CreatedAt.AddDays(10),
        ["FRA"],
        ["69"],
        ["72200000"],
        [],
        [],
        50,
        ["CPV ciblé"],
        "https://example.test/avis",
        "{}",
        CreatedAt);

    private static RiposteDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<RiposteDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
}
