namespace RiposteOS.Infrastructure.Consultations.Knowledge;

public sealed record ConsultationRetrievalResult(ConsultationEvidence[] Evidence, bool IsConfigured)
{
    public static ConsultationRetrievalResult NotConfigured { get; } = new([], false);
}

public sealed record ConsultationEvidence(
    Guid PassageId,
    double Score,
    Guid DocumentId,
    string DocumentName,
    int? PageNumber,
    string? SectionTitle,
    int Ordinal,
    string Text);

public sealed record ConsultationKnowledgeDocument(
    Guid DocumentId,
    string DocumentName,
    string Kind,
    bool IsIndexed);

public sealed record ConsultationKnowledgeSection(int? PageNumber, string? SectionTitle);
