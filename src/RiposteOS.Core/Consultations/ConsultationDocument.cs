namespace RiposteOS.Core.Consultations;

public sealed class ConsultationDocument
{
    public ConsultationDocument(
        Guid consultationId,
        Guid storedDocumentId,
        ConsultationDocumentKind kind,
        DateTimeOffset addedAt)
        : this(
            consultationId,
            storedDocumentId,
            kind,
            ConsultationDocumentKindOrigin.Manual,
            addedAt)
    {
    }

    public ConsultationDocument(
        Guid consultationId,
        Guid storedDocumentId,
        ConsultationDocumentKind kind,
        ConsultationDocumentKindOrigin kindOrigin,
        DateTimeOffset addedAt)
    {
        ConsultationId = ValidateIdentifier(consultationId, nameof(consultationId));
        StoredDocumentId = ValidateIdentifier(storedDocumentId, nameof(storedDocumentId));
        Kind = Enum.IsDefined(kind)
            ? kind
            : throw new ArgumentOutOfRangeException(nameof(kind));
        KindOrigin = Enum.IsDefined(kindOrigin)
            ? kindOrigin
            : throw new ArgumentOutOfRangeException(nameof(kindOrigin));
        AddedAt = addedAt;
    }

    public Guid ConsultationId { get; private set; }

    public Guid StoredDocumentId { get; private set; }

    public ConsultationDocumentKind Kind { get; private set; }

    public ConsultationDocumentKindOrigin KindOrigin { get; private set; }

    public DateTimeOffset AddedAt { get; private set; }

    public void ChangeKind(ConsultationDocumentKind kind)
    {
        Kind = Enum.IsDefined(kind)
            ? kind
            : throw new ArgumentOutOfRangeException(nameof(kind));
        KindOrigin = ConsultationDocumentKindOrigin.Manual;
    }

    public void ApplyAutomaticKind(ConsultationDocumentKind kind)
    {
        if (KindOrigin != ConsultationDocumentKindOrigin.Automatic)
        {
            return;
        }

        Kind = Enum.IsDefined(kind)
            ? kind
            : throw new ArgumentOutOfRangeException(nameof(kind));
    }

    private static Guid ValidateIdentifier(Guid value, string parameterName) =>
        value == Guid.Empty
            ? throw new ArgumentException("An identifier is required.", parameterName)
            : value;
}
