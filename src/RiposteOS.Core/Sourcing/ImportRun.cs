namespace RiposteOS.Core.Sourcing;

public sealed class ImportRun
{
    private ImportRun(
        Guid id,
        string source,
        ImportRunStatus status,
        DateTimeOffset queuedAt,
        DateTimeOffset? startedAt,
        DateTimeOffset? finishedAt,
        DateTimeOffset lastHeartbeatAt,
        DateOnly? currentPublicationDate,
        int fetched,
        int created,
        int updated,
        int skipped,
        string? errorMessage)
    {
        Id = id;
        Source = source;
        Status = status;
        QueuedAt = queuedAt;
        StartedAt = startedAt;
        FinishedAt = finishedAt;
        LastHeartbeatAt = lastHeartbeatAt;
        CurrentPublicationDate = currentPublicationDate;
        Fetched = fetched;
        Created = created;
        Updated = updated;
        Skipped = skipped;
        ErrorMessage = errorMessage;
    }

    public ImportRun(string source, DateTimeOffset queuedAt)
        : this(
            Guid.Empty,
            SourcingSource.Normalize(source),
            ImportRunStatus.Queued,
            queuedAt,
            null,
            null,
            queuedAt,
            null,
            0,
            0,
            0,
            0,
            null)
    {
    }

    public Guid Id { get; private set; }

    public string Source { get; private set; }

    public ImportRunStatus Status { get; private set; }

    public DateTimeOffset QueuedAt { get; private set; }

    public DateTimeOffset? StartedAt { get; private set; }

    public DateTimeOffset? FinishedAt { get; private set; }

    public DateTimeOffset LastHeartbeatAt { get; private set; }

    public DateOnly? CurrentPublicationDate { get; private set; }

    public int Fetched { get; private set; }

    public int Created { get; private set; }

    public int Updated { get; private set; }

    public int Skipped { get; private set; }

    public string? ErrorMessage { get; private set; }

    public bool TryStart(DateTimeOffset startedAt)
    {
        EnsureChronological(startedAt);

        if (Status == ImportRunStatus.Running)
        {
            LastHeartbeatAt = startedAt;
            return true;
        }

        if (Status != ImportRunStatus.Queued)
        {
            return false;
        }

        Status = ImportRunStatus.Running;
        StartedAt = startedAt;
        LastHeartbeatAt = startedAt;
        return true;
    }

    public void RecordProgress(
        DateOnly publicationDate,
        int fetched,
        int created,
        int updated,
        int skipped,
        DateTimeOffset heartbeatAt)
    {
        if (Status != ImportRunStatus.Running)
        {
            throw new InvalidOperationException("Progress can only be recorded for a running import.");
        }

        if (fetched < 0 || created < 0 || updated < 0 || skipped < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(fetched), "Progress counters cannot be negative.");
        }

        if (created + updated + skipped > fetched)
        {
            throw new ArgumentException("Processed counters cannot exceed fetched records.", nameof(fetched));
        }

        EnsureChronological(heartbeatAt);
        CurrentPublicationDate = publicationDate;
        Fetched += fetched;
        Created += created;
        Updated += updated;
        Skipped += skipped;
        LastHeartbeatAt = heartbeatAt;
    }

    public void Complete(DateTimeOffset finishedAt)
    {
        if (Status != ImportRunStatus.Running)
        {
            throw new InvalidOperationException("Only a running import can be completed.");
        }

        EnsureChronological(finishedAt);
        Status = Skipped > 0
            ? ImportRunStatus.PartiallyFailed
            : ImportRunStatus.Succeeded;
        FinishedAt = finishedAt;
        LastHeartbeatAt = finishedAt;
    }

    public void Fail(string errorMessage, DateTimeOffset finishedAt)
    {
        if (Status is not (ImportRunStatus.Queued or ImportRunStatus.Running))
        {
            throw new InvalidOperationException("A finished import cannot fail again.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);
        EnsureChronological(finishedAt);
        Status = ImportRunStatus.Failed;
        ErrorMessage = errorMessage.Trim();
        FinishedAt = finishedAt;
        LastHeartbeatAt = finishedAt;
    }

    private void EnsureChronological(DateTimeOffset timestamp)
    {
        if (timestamp < LastHeartbeatAt)
        {
            throw new ArgumentOutOfRangeException(nameof(timestamp), "Import timestamps must be chronological.");
        }
    }
}
