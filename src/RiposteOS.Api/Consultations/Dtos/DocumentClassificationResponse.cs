namespace RiposteOS.Api.Consultations.Dtos;

public sealed record DocumentClassificationResponse(
    string Status,
    string? ProposedKind,
    string? Confidence,
    DateTimeOffset? QueuedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    DateTimeOffset? FailedAt,
    string? ProviderName,
    string? Model,
    string? ErrorMessage);
