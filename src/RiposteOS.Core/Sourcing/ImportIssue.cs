namespace RiposteOS.Core.Sourcing;

public sealed class ImportIssue
{
    private ImportIssue(
        Guid id,
        Guid runId,
        string source,
        string? sourceId,
        string errorCode,
        string rawPayload,
        DateTimeOffset createdAt,
        DateTimeOffset? resolvedAt)
    {
        Id = id;
        RunId = runId;
        Source = source;
        SourceId = sourceId;
        ErrorCode = errorCode;
        RawPayload = rawPayload;
        CreatedAt = createdAt;
        ResolvedAt = resolvedAt;
    }

    public ImportIssue(
        Guid runId,
        string source,
        string? sourceId,
        string errorCode,
        string rawPayload,
        DateTimeOffset createdAt)
        : this(
            Guid.Empty,
            runId == Guid.Empty ? throw new ArgumentException("A run identifier is required.", nameof(runId)) : runId,
            SourcingSource.Normalize(source),
            string.IsNullOrWhiteSpace(sourceId) ? null : sourceId.Trim(),
            NormalizeRequired(errorCode, nameof(errorCode)),
            NormalizeRequired(rawPayload, nameof(rawPayload)),
            createdAt,
            null)
    {
    }

    public Guid Id { get; private set; }

    public Guid RunId { get; private set; }

    public string Source { get; private set; }

    public string? SourceId { get; private set; }

    public string ErrorCode { get; private set; }

    public string RawPayload { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? ResolvedAt { get; private set; }

    public void Resolve(DateTimeOffset resolvedAt)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(resolvedAt, CreatedAt);

        ResolvedAt ??= resolvedAt;
    }

    private static string NormalizeRequired(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        return value.Trim();
    }
}
