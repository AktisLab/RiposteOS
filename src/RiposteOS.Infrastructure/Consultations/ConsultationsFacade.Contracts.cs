using RiposteOS.Core.Consultations;

namespace RiposteOS.Infrastructure.Consultations;

public sealed record ConsultationResult
{
    public required Guid Id { get; init; }

    public required Guid? OpportunityId { get; init; }

    public required string Title { get; init; }

    public required string Buyer { get; init; }

    public required DateTimeOffset? ResponseDeadline { get; init; }

    public required string? NoticeUrl { get; init; }

    public required string? Source { get; init; }

    public required string? SourceId { get; init; }

    public required int DocumentCount { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public required DateTimeOffset UpdatedAt { get; init; }
}

public sealed record ConsultationPageResult(
    ConsultationResult[] Items,
    int TotalCount,
    string[] ValidationErrors);

public sealed record ConsultationPromotionResult(ConsultationResult? Consultation, bool Created);

public enum ConsultationDocumentAttachmentStatus
{
    ConsultationNotFound,
    StoredDocumentNotFound,
    Created,
    Existing,
}

public sealed record ConsultationDocumentAttachmentResult(
    ConsultationDocumentAttachmentStatus Status,
    ConsultationDocumentResult? Document);

public sealed record ConsultationDocumentResult(
    Guid Id,
    string OriginalFileName,
    string ContentType,
    long Size,
    DateTimeOffset CreatedAt,
    ConsultationDocumentKind Kind,
    DateTimeOffset AddedAt);
