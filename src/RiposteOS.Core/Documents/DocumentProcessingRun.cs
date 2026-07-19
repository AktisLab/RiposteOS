namespace RiposteOS.Core.Documents;

public sealed class DocumentProcessingRun
{
    public const int MaximumErrorMessageLength = 1_000;

    private DocumentProcessingRun(
        Guid id,
        Guid storedDocumentId,
        DocumentProcessingStatus status,
        DateTimeOffset queuedAt,
        DateTimeOffset? startedAt,
        DateTimeOffset? completedAt,
        DateTimeOffset? failedAt,
        int pageCount,
        int passageCount,
        string? errorMessage)
    {
        Id = id;
        StoredDocumentId = storedDocumentId;
        Status = status;
        QueuedAt = queuedAt;
        StartedAt = startedAt;
        CompletedAt = completedAt;
        FailedAt = failedAt;
        PageCount = pageCount;
        PassageCount = passageCount;
        ErrorMessage = errorMessage;
    }

    public DocumentProcessingRun(Guid storedDocumentId, DateTimeOffset queuedAt)
        : this(
            Guid.Empty,
            ValidateIdentifier(storedDocumentId, nameof(storedDocumentId)),
            DocumentProcessingStatus.Queued,
            queuedAt,
            null,
            null,
            null,
            0,
            0,
            null)
    {
    }

    public Guid Id { get; private set; }

    public Guid StoredDocumentId { get; private set; }

    public StoredDocument? StoredDocument { get; private set; }

    public DocumentProcessingStatus Status { get; private set; }

    public DateTimeOffset QueuedAt { get; private set; }

    public DateTimeOffset? StartedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public DateTimeOffset? FailedAt { get; private set; }

    public int PageCount { get; private set; }

    public int PassageCount { get; private set; }

    public string? ErrorMessage { get; private set; }

    public bool TryStart(DateTimeOffset startedAt)
    {
        if (Status != DocumentProcessingStatus.Queued)
        {
            return false;
        }

        EnsureNotBefore(startedAt, QueuedAt);
        Status = DocumentProcessingStatus.Running;
        StartedAt = startedAt;
        return true;
    }

    public void Complete(int pageCount, int passageCount, DateTimeOffset completedAt)
    {
        EnsureRunning();
        ValidateCounters(pageCount, passageCount);
        EnsureNotBefore(completedAt, StartedAt!.Value);

        Status = DocumentProcessingStatus.Completed;
        PageCount = pageCount;
        PassageCount = passageCount;
        CompletedAt = completedAt;
        FailedAt = null;
        ErrorMessage = null;
    }

    public void Fail(string errorMessage, DateTimeOffset failedAt)
    {
        if (Status is not (DocumentProcessingStatus.Queued or DocumentProcessingStatus.Running))
        {
            throw new InvalidOperationException("Only a queued or running document processing run can fail.");
        }

        EnsureNotBefore(failedAt, StartedAt ?? QueuedAt);
        var normalizedErrorMessage = NormalizeErrorMessage(errorMessage);
        Status = DocumentProcessingStatus.Failed;
        FailedAt = failedAt;
        ErrorMessage = normalizedErrorMessage;
    }

    public void Retry(DateTimeOffset queuedAt)
    {
        if (Status != DocumentProcessingStatus.Failed)
        {
            throw new InvalidOperationException("Only a failed document processing run can be retried.");
        }

        EnsureNotBefore(queuedAt, FailedAt!.Value);
        Status = DocumentProcessingStatus.Queued;
        QueuedAt = queuedAt;
        StartedAt = null;
        CompletedAt = null;
        FailedAt = null;
        PageCount = 0;
        PassageCount = 0;
        ErrorMessage = null;
    }

    private void EnsureRunning()
    {
        if (Status != DocumentProcessingStatus.Running)
        {
            throw new InvalidOperationException("Only a running document processing run can be completed.");
        }
    }

    private static Guid ValidateIdentifier(Guid value, string parameterName) =>
        value == Guid.Empty
            ? throw new ArgumentException("An identifier is required.", parameterName)
            : value;

    private static void ValidateCounters(int pageCount, int passageCount)
    {
        if (pageCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pageCount), "Page count cannot be negative.");
        }

        if (passageCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(passageCount), "Passage count cannot be negative.");
        }
    }

    private static string NormalizeErrorMessage(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var normalized = value.Trim();
        if (normalized.Length > MaximumErrorMessageLength)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "The error message is too long.");
        }

        return normalized;
    }

    private static void EnsureNotBefore(DateTimeOffset timestamp, DateTimeOffset reference)
    {
        if (timestamp < reference)
        {
            throw new ArgumentOutOfRangeException(nameof(timestamp), "Document processing timestamps must be chronological.");
        }
    }
}
