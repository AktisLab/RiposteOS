using RiposteOS.Core.Consultations;
using RiposteOS.Infrastructure.Consultations.Knowledge;

namespace RiposteOS.Infrastructure.Consultations.Assistant;

public sealed record ConsultationAssistantConversationSummary(Guid Id, string Title, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, DateTimeOffset? ArchivedAt);
public sealed record ConsultationAssistantConversationDetails(ConsultationAssistantConversationSummary Conversation, ConsultationAssistantMessageResult[] Messages);
public sealed record ConsultationAssistantAnswerDetails(string? Status, string[]? Gaps, string[]? FollowUps, string? ReasoningSummary = null);
public sealed record ConsultationAssistantMessageResult(Guid Id, ConsultationAssistantMessageRole Role, string? Content, ConsultationAssistantMessageStatus Status, string? ErrorMessage, DateTimeOffset CreatedAt, DateTimeOffset? CompletedAt, DateTimeOffset? FailedAt, string? ProviderName, string? Model, ConsultationEvidence[] Evidence, ConsultationAssistantAnswerDetails? Details);

public sealed record ConsultationAssistantStreamEvent(string Type, Guid? MessageId, string? Delta, string? Error, ConsultationAssistantMessageResult? Message, string? Activity = null)
{
    public static ConsultationAssistantStreamEvent Started(Guid id) => new("message_started", id, null, null, null);
    public static ConsultationAssistantStreamEvent AnswerDelta(string delta, Guid id) => new("answer_delta", id, delta, null, null);
    public static ConsultationAssistantStreamEvent ReasoningDelta(string delta, Guid id) => new("reasoning_delta", id, delta, null, null);
    public static ConsultationAssistantStreamEvent Completed(ConsultationAssistantMessageResult message) => new("message_completed", message.Id, null, null, message);
    public static ConsultationAssistantStreamEvent Failed(string error, Guid? id = null) => new("message_failed", id, null, error, null);
    public static ConsultationAssistantStreamEvent Cancelled(Guid id) => new("message_cancelled", id, null, null, null);
    public static ConsultationAssistantStreamEvent Progress(string activity) => new("activity", null, null, null, null, activity);
}
