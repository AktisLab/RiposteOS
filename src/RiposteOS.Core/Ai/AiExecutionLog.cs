namespace RiposteOS.Core.Ai;

public sealed class AiExecutionLog
{
    public const int MaximumProviderNameLength = 200;
    public const int MaximumModelLength = 200;
    public const int MaximumErrorMessageLength = 1_000;

    private AiExecutionLog(
        Guid id,
        AiExecutionOperation operation,
        AiExecutionSubjectKind subjectKind,
        Guid subjectId,
        string subjectLabel,
        Guid? correlationId,
        DateTimeOffset startedAt,
        string? providerName,
        string? model,
        Guid? providerId,
        AiExecutionStatus status,
        DateTimeOffset? completedAt,
        DateTimeOffset? failedAt,
        string? errorMessage)
    {
        Id = id;
        Operation = operation;
        SubjectKind = subjectKind;
        SubjectId = subjectId;
        SubjectLabel = subjectLabel;
        CorrelationId = correlationId;
        StartedAt = startedAt;
        ProviderName = providerName;
        Model = model;
        ProviderId = providerId;
        Status = status;
        CompletedAt = completedAt;
        FailedAt = failedAt;
        ErrorMessage = errorMessage;
    }

    public AiExecutionLog(
        AiExecutionOperation operation,
        AiExecutionSubject subject,
        Guid? correlationId,
        string? providerName,
        string? model,
        Guid? providerId,
        DateTimeOffset startedAt)
        : this(
            Guid.Empty,
            ValidateOperation(operation),
            subject.Kind,
            subject.Id,
            subject.Label,
            correlationId,
            startedAt,
            Optional(providerName, MaximumProviderNameLength, nameof(providerName)),
            Optional(model, MaximumModelLength, nameof(model)),
            providerId,
            AiExecutionStatus.Running,
            null,
            null,
            null)
    {
    }

    public Guid Id { get; private set; }

    public AiExecutionOperation Operation { get; private set; }

    public AiExecutionSubjectKind SubjectKind { get; private set; }

    public Guid SubjectId { get; private set; }

    public string SubjectLabel { get; private set; }

    public Guid? CorrelationId { get; private set; }

    public Guid? ProviderId { get; private set; }

    public string? ProviderName { get; private set; }

    public string? Model { get; private set; }

    public AiExecutionStatus Status { get; private set; }

    public DateTimeOffset StartedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public DateTimeOffset? FailedAt { get; private set; }

    public string? ErrorMessage { get; private set; }

    public void SetProvider(Guid providerId, string providerName, string model)
    {
        EnsureRunning();
        ProviderId = RequiredId(providerId, nameof(providerId));
        ProviderName = Required(providerName, MaximumProviderNameLength, nameof(providerName));
        Model = Required(model, MaximumModelLength, nameof(model));
    }

    public void Complete(DateTimeOffset completedAt)
    {
        EnsureRunning();
        EnsureNotBefore(completedAt);
        Status = AiExecutionStatus.Completed;
        CompletedAt = completedAt;
        FailedAt = null;
        ErrorMessage = null;
    }

    public void Fail(string errorMessage, DateTimeOffset failedAt, bool notConfigured = false)
    {
        EnsureRunning();
        EnsureNotBefore(failedAt);
        Status = notConfigured ? AiExecutionStatus.NotConfigured : AiExecutionStatus.Failed;
        FailedAt = failedAt;
        CompletedAt = null;
        ErrorMessage = Required(errorMessage, MaximumErrorMessageLength, nameof(errorMessage));
    }

    private void EnsureRunning()
    {
        if (Status != AiExecutionStatus.Running)
        {
            throw new InvalidOperationException("Only a running AI execution can change state.");
        }
    }

    private void EnsureNotBefore(DateTimeOffset timestamp)
    {
        if (timestamp < StartedAt)
        {
            throw new ArgumentOutOfRangeException(nameof(timestamp), "Execution timestamps must be chronological.");
        }
    }

    private static AiExecutionOperation ValidateOperation(AiExecutionOperation operation)
    {
        if (!Enum.IsDefined(operation))
        {
            throw new ArgumentOutOfRangeException(nameof(operation));
        }

        return operation;
    }

    private static Guid RequiredId(Guid value, string parameterName) =>
        value == Guid.Empty
            ? throw new ArgumentException("An identifier is required.", parameterName)
            : value;

    private static string Required(string value, int maximumLength, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var normalized = value.Trim();
        if (normalized.Length > maximumLength)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }

        return normalized;
    }

    private static string? Optional(string? value, int maximumLength, string parameterName) =>
        value is null ? null : Required(value, maximumLength, parameterName);
}
