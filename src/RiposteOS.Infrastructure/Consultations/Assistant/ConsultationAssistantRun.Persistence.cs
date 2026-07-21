using System.Text.Json;
using System.Threading.Channels;
using RiposteOS.Core.Consultations;
using RiposteOS.Infrastructure.Ai.Execution;
using RiposteOS.Infrastructure.Ai.Tasks;
using RiposteOS.Infrastructure.Consultations.Knowledge;

namespace RiposteOS.Infrastructure.Consultations.Assistant;

public sealed partial class ConsultationAssistantRun
{
    private async Task CompleteAsync(
        ConsultationAssistantMessage assistant,
        AiTaskClient chat,
        string answer,
        ConsultationEvidence[] cited,
        string status,
        string reasoningSummary,
        AiExecutionScope execution,
        ChannelWriter<ConsultationAssistantStreamEvent> events,
        CancellationToken cancellationToken,
        bool recordOutput = true)
    {
        if (recordOutput) await execution.RecordOutputAsync(JsonSerializer.Serialize(new { Answer = answer }), cancellationToken);
        var details = new ConsultationAssistantAnswerDetails(status, [], [], string.IsNullOrWhiteSpace(reasoningSummary) ? null : reasoningSummary.Trim());
        assistant.Complete(answer, chat.ProviderName, chat.Model, JsonSerializer.Serialize(details), timeProvider.GetUtcNow());
        dbContext.Set<ConsultationAssistantMessageCitation>().AddRange(cited.Select(item => new ConsultationAssistantMessageCitation(assistant.Id, item.PassageId)));
        await dbContext.SaveChangesAsync(cancellationToken);
        await execution.CompleteAsync(cancellationToken);
        await events.WriteAsync(ConsultationAssistantStreamEvent.Completed(ToMessage(assistant, cited)), cancellationToken);
    }

    private async Task FailAsync(
        ConsultationAssistantMessage assistant,
        string error,
        bool cancelled,
        CancellationToken cancellationToken)
    {
        assistant.Fail(error, timeProvider.GetUtcNow(), cancelled);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string ProvisionalTitle(string question)
    {
        var normalized = string.Join(' ', question.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return normalized.Length <= 80 ? normalized : string.Concat(normalized.AsSpan(0, 77), "...");
    }

    private static ConsultationAssistantMessageResult ToMessage(
        ConsultationAssistantMessage message,
        ConsultationEvidence[] evidence) =>
        new(message.Id, message.Role, message.Content, message.Status, message.ErrorMessage, message.CreatedAt, message.CompletedAt, message.FailedAt, message.ProviderName, message.Model, evidence, message.StructuredContent is null ? null : JsonSerializer.Deserialize<ConsultationAssistantAnswerDetails>(message.StructuredContent));
}
