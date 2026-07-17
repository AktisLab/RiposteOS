namespace RiposteOS.Infrastructure.Sourcing;

public sealed record SourceImportIssue(
    string? SourceId,
    string ErrorCode,
    string RawPayload);
