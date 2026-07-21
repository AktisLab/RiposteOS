namespace RiposteOS.Infrastructure.Consultations.Knowledge;

public sealed record ReferencedPassage(
    string Reference,
    Guid DocumentId,
    string DocumentName,
    int? PageNumber,
    string? SectionTitle,
    int Ordinal,
    string Text);
