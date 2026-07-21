namespace RiposteOS.Core.Documents;

public sealed class DocumentPassageEmbedding
{
    public const int ExpectedDimension = 1024;

    public DocumentPassageEmbedding(Guid documentPassageId, string textHash, string providerName, string model, DateTimeOffset queuedAt)
    {
        DocumentPassageId = documentPassageId == Guid.Empty ? throw new ArgumentException("A passage is required.", nameof(documentPassageId)) : documentPassageId;
        TextHash = Required(textHash, 64, nameof(textHash));
        ProviderName = Required(providerName, 200, nameof(providerName));
        Model = Required(model, 200, nameof(model));
        QueuedAt = queuedAt;
    }

    public Guid Id { get; private set; }
    public Guid DocumentPassageId { get; private set; }
    public string TextHash { get; private set; }
    public string ProviderName { get; private set; }
    public string Model { get; private set; }
    public int Dimension { get; private set; } = ExpectedDimension;
    public float[]? Embedding { get; private set; }
    public DocumentPassageEmbeddingStatus Status { get; private set; } = DocumentPassageEmbeddingStatus.Queued;
    public DateTimeOffset QueuedAt { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public DateTimeOffset? FailedAt { get; private set; }
    public string? ErrorMessage { get; private set; }

    public bool TryStart(DateTimeOffset now)
    {
        if (Status is not (DocumentPassageEmbeddingStatus.Queued or DocumentPassageEmbeddingStatus.Failed)) return false;
        Status = DocumentPassageEmbeddingStatus.Running;
        StartedAt = now;
        FailedAt = null;
        ErrorMessage = null;
        return true;
    }

    public void Complete(float[] embedding, DateTimeOffset now)
    {
        if (Status != DocumentPassageEmbeddingStatus.Running) throw new InvalidOperationException("The embedding is not running.");
        if (embedding.Length != ExpectedDimension) throw new ArgumentException($"The embedding must contain exactly {ExpectedDimension} dimensions.", nameof(embedding));
        Embedding = embedding.ToArray();
        Status = DocumentPassageEmbeddingStatus.Completed;
        CompletedAt = now;
        ErrorMessage = null;
    }

    public void Fail(string errorMessage, DateTimeOffset now)
    {
        if (Status is DocumentPassageEmbeddingStatus.Completed) return;
        Status = DocumentPassageEmbeddingStatus.Failed;
        FailedAt = now;
        ErrorMessage = Required(errorMessage, 500, nameof(errorMessage));
    }

    public bool Matches(string textHash, string providerName, string model) =>
        Status == DocumentPassageEmbeddingStatus.Completed
        && TextHash == textHash
        && ProviderName == providerName
        && Model == model
        && Dimension == ExpectedDimension;

    private static string Required(string value, int max, string name) => string.IsNullOrWhiteSpace(value) || value.Trim().Length > max ? throw new ArgumentException("A valid value is required.", name) : value.Trim();
}
