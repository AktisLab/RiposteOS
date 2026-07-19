namespace RiposteOS.Api.Consultations.Dtos;

public sealed record DocumentAnalysisResponse(
    string Status,
    DateTimeOffset? QueuedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    DateTimeOffset? FailedAt,
    int PageCount,
    int PassageCount,
    string? ErrorMessage);
