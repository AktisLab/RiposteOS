namespace RiposteOS.Api.Sourcing.Dtos;

public sealed record ImportRunResponse(
    Guid Id,
    string Source,
    string Status,
    DateTimeOffset QueuedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt,
    DateOnly? CurrentPublicationDate,
    int Fetched,
    int Created,
    int Updated,
    int Skipped,
    string? ErrorMessage);
