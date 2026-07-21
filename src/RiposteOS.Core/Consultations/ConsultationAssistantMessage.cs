namespace RiposteOS.Core.Consultations;

public sealed class ConsultationAssistantMessage
{
    public const int MaximumContentLength = 16_000;
    public const int MaximumErrorLength = 500;

    private ConsultationAssistantMessage(Guid conversationId, ConsultationAssistantMessageRole role, string content, DateTimeOffset createdAt)
    {
        ConversationId = conversationId == Guid.Empty ? throw new ArgumentException("A conversation is required.", nameof(conversationId)) : conversationId;
        Role = role;
        Content = Required(content, MaximumContentLength, nameof(content));
        CreatedAt = createdAt;
        Status = ConsultationAssistantMessageStatus.Completed;
        CompletedAt = createdAt;
    }

    private ConsultationAssistantMessage(Guid conversationId, DateTimeOffset createdAt)
    {
        ConversationId = conversationId == Guid.Empty ? throw new ArgumentException("A conversation is required.", nameof(conversationId)) : conversationId;
        Role = ConsultationAssistantMessageRole.Assistant;
        Content = null;
        CreatedAt = createdAt;
        Status = ConsultationAssistantMessageStatus.Pending;
    }

    public Guid Id { get; private set; }
    public Guid ConversationId { get; private set; }
    public ConsultationAssistantMessageRole Role { get; private set; }
    public string? Content { get; private set; }
    public ConsultationAssistantMessageStatus Status { get; private set; }
    public string? ProviderName { get; private set; }
    public string? Model { get; private set; }
    public string? ErrorMessage { get; private set; }
    public string? StructuredContent { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public DateTimeOffset? FailedAt { get; private set; }

    public static ConsultationAssistantMessage CreateUser(Guid conversationId, string content, DateTimeOffset now) => new(conversationId, ConsultationAssistantMessageRole.User, content, now);
    public static ConsultationAssistantMessage StartAssistant(Guid conversationId, DateTimeOffset now) => new(conversationId, now);

    public void Complete(string content, string providerName, string model, DateTimeOffset now)
        => Complete(content, providerName, model, null, now);

    public void Complete(string content, string providerName, string model, string? structuredContent, DateTimeOffset now)
    {
        if (Status != ConsultationAssistantMessageStatus.Pending) throw new InvalidOperationException("The assistant message is no longer pending.");
        Content = Required(content, MaximumContentLength, nameof(content));
        ProviderName = Required(providerName, 200, nameof(providerName));
        Model = Required(model, 200, nameof(model));
        StructuredContent = structuredContent is null ? null : Required(structuredContent, MaximumContentLength, nameof(structuredContent));
        Status = ConsultationAssistantMessageStatus.Completed;
        CompletedAt = now;
    }

    public void Fail(string errorMessage, DateTimeOffset now, bool cancelled = false)
    {
        if (Status != ConsultationAssistantMessageStatus.Pending) return;
        Status = cancelled ? ConsultationAssistantMessageStatus.Cancelled : ConsultationAssistantMessageStatus.Failed;
        ErrorMessage = Required(errorMessage, MaximumErrorLength, nameof(errorMessage));
        FailedAt = now;
    }

    private static string Required(string value, int max, string name) => string.IsNullOrWhiteSpace(value) || value.Trim().Length > max ? throw new ArgumentException("A valid value is required.", name) : value.Trim();
}
