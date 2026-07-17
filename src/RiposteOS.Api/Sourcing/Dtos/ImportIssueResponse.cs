namespace RiposteOS.Api.Sourcing.Dtos;

public sealed record ImportIssueResponse(
    Guid Id,
    Guid RunId,
    string Source,
    string? SourceId,
    string ErrorCode,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ResolvedAt);
