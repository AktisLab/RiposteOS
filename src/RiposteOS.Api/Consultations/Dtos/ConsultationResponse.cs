namespace RiposteOS.Api.Consultations.Dtos;

public sealed record ConsultationResponse(
    Guid Id,
    Guid? OpportunityId,
    string Title,
    string Buyer,
    DateTimeOffset? ResponseDeadline,
    string? NoticeUrl,
    string? Source,
    string? SourceId,
    int DocumentCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
