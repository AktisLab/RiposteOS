using RiposteOS.Core.Consultations;

namespace RiposteOS.Core.Ai;

public sealed class ConsultationDocumentClassification
{
    public ConsultationDocumentClassification(Guid id, Guid consultationId, Guid storedDocumentId, DocumentClassificationStatus status, ConsultationDocumentKind? proposedKind, DocumentClassificationConfidence? confidence, string? providerName, string? model, DateTimeOffset queuedAt, DateTimeOffset? startedAt, DateTimeOffset? completedAt, DateTimeOffset? failedAt, string? errorMessage)
    {
        Id = id; ConsultationId = consultationId; StoredDocumentId = storedDocumentId; Status = status; ProposedKind = proposedKind; Confidence = confidence; ProviderName = providerName; Model = model; QueuedAt = queuedAt; StartedAt = startedAt; CompletedAt = completedAt; FailedAt = failedAt; ErrorMessage = errorMessage;
    }
    public ConsultationDocumentClassification(Guid consultationId, Guid storedDocumentId, DateTimeOffset queuedAt)
    {
        ConsultationId = RequiredId(consultationId, nameof(consultationId)); StoredDocumentId = RequiredId(storedDocumentId, nameof(storedDocumentId));
        Status = DocumentClassificationStatus.Queued; QueuedAt = queuedAt;
    }
    public Guid Id { get; private set; }
    public Guid ConsultationId { get; private set; }
    public Guid StoredDocumentId { get; private set; }
    public DocumentClassificationStatus Status { get; private set; }
    public ConsultationDocumentKind? ProposedKind { get; private set; }
    public DocumentClassificationConfidence? Confidence { get; private set; }
    public IReadOnlyList<Guid> EvidencePassageIds => evidencePassageIds;
    private List<Guid> evidencePassageIds = [];
    public string? ProviderName { get; private set; }
    public Guid? ProviderId { get; private set; }
    public string? Model { get; private set; }
    public DateTimeOffset QueuedAt { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public DateTimeOffset? FailedAt { get; private set; }
    public string? ErrorMessage { get; private set; }
    public bool TryStart(DateTimeOffset now) { if (Status != DocumentClassificationStatus.Queued) return false; Status = DocumentClassificationStatus.Running; StartedAt = now; return true; }
    public void Complete(ConsultationDocumentKind kind, DocumentClassificationConfidence confidence, IEnumerable<Guid> evidence, Guid providerId, string providerName, string model, DateTimeOffset now)
    {
        if (Status != DocumentClassificationStatus.Running) throw new InvalidOperationException("Only a running classification can complete.");
        if (!Enum.IsDefined(kind) || !Enum.IsDefined(confidence)) throw new ArgumentOutOfRangeException(nameof(kind));
        var ids = evidence.Distinct().ToList(); if (ids.Count is < 1 or > 3 || ids.Any(id => id == Guid.Empty)) throw new ArgumentException("One to three evidence passages are required.", nameof(evidence));
        if (providerId == Guid.Empty) throw new ArgumentException("A provider is required.", nameof(providerId));
        ProposedKind = kind; Confidence = confidence; evidencePassageIds = ids; ProviderId = providerId; ProviderName = Required(providerName, nameof(providerName)); Model = Required(model, nameof(model)); Status = DocumentClassificationStatus.Completed; CompletedAt = now; ErrorMessage = null;
    }
    public void Fail(string message, DateTimeOffset now, bool notConfigured = false) { if (Status is not (DocumentClassificationStatus.Queued or DocumentClassificationStatus.Running)) throw new InvalidOperationException("Only a queued or running classification can fail."); Status = notConfigured ? DocumentClassificationStatus.NotConfigured : DocumentClassificationStatus.Failed; FailedAt = now; ErrorMessage = Required(message, nameof(message)); }
    public void Retry(DateTimeOffset now) { if (Status is not (DocumentClassificationStatus.Failed or DocumentClassificationStatus.NotConfigured)) throw new InvalidOperationException("Only a failed classification can be retried."); Status = DocumentClassificationStatus.Queued; QueuedAt = now; StartedAt = null; FailedAt = null; ErrorMessage = null; }
    private static Guid RequiredId(Guid value, string name) => value == Guid.Empty ? throw new ArgumentException("An identifier is required.", name) : value;
    private static string Required(string value, string name) => string.IsNullOrWhiteSpace(value) || value.Trim().Length > 1000 ? throw new ArgumentException("A valid value is required.", name) : value.Trim();
}
