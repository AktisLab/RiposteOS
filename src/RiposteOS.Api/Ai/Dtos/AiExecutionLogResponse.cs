namespace RiposteOS.Api.Ai.Dtos;

public sealed record AiExecutionLogResponse(
    Guid Id,
    string Operation,
    string Status,
    string SubjectKind,
    Guid SubjectId,
    string SubjectLabel,
    Guid? CorrelationId,
    Guid? ProviderId,
    string? ProviderName,
    string? Model,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    DateTimeOffset? FailedAt,
    string? ErrorMessage);
