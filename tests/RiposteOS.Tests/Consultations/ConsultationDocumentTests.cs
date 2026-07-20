using RiposteOS.Core.Consultations;

namespace RiposteOS.Tests.Consultations;

public sealed class ConsultationDocumentTests
{
    [Fact]
    public void LinkStoresTheBusinessKindAndAdditionDate()
    {
        var consultationId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var addedAt = new DateTimeOffset(2026, 7, 17, 12, 0, 0, TimeSpan.Zero);

        var link = new ConsultationDocument(
            consultationId,
            documentId,
            ConsultationDocumentKind.TechnicalSpecifications,
            addedAt);

        Assert.Equal(consultationId, link.ConsultationId);
        Assert.Equal(documentId, link.StoredDocumentId);
        Assert.Equal(ConsultationDocumentKind.TechnicalSpecifications, link.Kind);
        Assert.Equal(ConsultationDocumentKindOrigin.Manual, link.KindOrigin);
        Assert.Equal(addedAt, link.AddedAt);
    }

    [Fact]
    public void EmptyIdentifiersAndUnknownKindAreRejected()
    {
        Assert.Throws<ArgumentException>(() => new ConsultationDocument(
            Guid.Empty, Guid.NewGuid(), ConsultationDocumentKind.Other, DateTimeOffset.UtcNow));
        Assert.Throws<ArgumentException>(() => new ConsultationDocument(
            Guid.NewGuid(), Guid.Empty, ConsultationDocumentKind.Other, DateTimeOffset.UtcNow));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ConsultationDocument(
            Guid.NewGuid(), Guid.NewGuid(), (ConsultationDocumentKind)999, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void BusinessKindCanBeChangedAndIsValidated()
    {
        var link = new ConsultationDocument(
            Guid.NewGuid(),
            Guid.NewGuid(),
            ConsultationDocumentKind.Other,
            DateTimeOffset.UtcNow);

        link.ChangeKind(ConsultationDocumentKind.Pricing);

        Assert.Equal(ConsultationDocumentKind.Pricing, link.Kind);
        Assert.Throws<ArgumentOutOfRangeException>(() => link.ChangeKind((ConsultationDocumentKind)999));
    }

    [Fact]
    public void AutomaticKindCanBeAppliedOnlyUntilHumanCorrection()
    {
        var link = new ConsultationDocument(
            Guid.NewGuid(),
            Guid.NewGuid(),
            ConsultationDocumentKind.Other,
            ConsultationDocumentKindOrigin.Automatic,
            DateTimeOffset.UtcNow);

        link.ApplyAutomaticKind(ConsultationDocumentKind.TechnicalSpecifications);
        link.ChangeKind(ConsultationDocumentKind.Pricing);
        link.ApplyAutomaticKind(ConsultationDocumentKind.AdministrativeSpecifications);

        Assert.Equal(ConsultationDocumentKind.Pricing, link.Kind);
        Assert.Equal(ConsultationDocumentKindOrigin.Manual, link.KindOrigin);
    }
}
