using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RiposteOS.Core.Consultations;
using RiposteOS.Infrastructure.Consultations.Knowledge;
using RiposteOS.Infrastructure.Persistence;

namespace RiposteOS.Infrastructure.Consultations.Assistant;

public sealed class ConsultationAssistantFacade(
    RiposteDbContext dbContext,
    ConsultationAssistantRun run,
    ConsultationKnowledgeFacade knowledge,
    TimeProvider timeProvider)
{
    public async Task<ConsultationAssistantConversationSummary[]?> ListAsync(Guid consultationId, CancellationToken cancellationToken)
    {
        if (!await dbContext.Set<Consultation>().AsNoTracking().AnyAsync(item => item.Id == consultationId, cancellationToken)) return null;
        return await dbContext.Set<ConsultationAssistantConversation>().AsNoTracking().Where(item => item.ConsultationId == consultationId).OrderBy(item => item.ArchivedAt != null).ThenByDescending(item => item.UpdatedAt).ThenBy(item => item.Id).Select(item => new ConsultationAssistantConversationSummary(item.Id, item.Title, item.CreatedAt, item.UpdatedAt, item.ArchivedAt)).ToArrayAsync(cancellationToken);
    }

    public async Task<ConsultationAssistantConversationSummary?> CreateAsync(Guid consultationId, string? title, CancellationToken cancellationToken)
    {
        if (!await dbContext.Set<Consultation>().AsNoTracking().AnyAsync(item => item.Id == consultationId, cancellationToken)) return null;
        var conversation = new ConsultationAssistantConversation(consultationId, string.IsNullOrWhiteSpace(title) ? "Nouvelle conversation" : title, timeProvider.GetUtcNow());
        dbContext.Add(conversation);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new ConsultationAssistantConversationSummary(conversation.Id, conversation.Title, conversation.CreatedAt, conversation.UpdatedAt, conversation.ArchivedAt);
    }

    public async Task<ConsultationAssistantConversationDetails?> GetAsync(Guid consultationId, Guid conversationId, CancellationToken cancellationToken)
    {
        var conversation = await dbContext.Set<ConsultationAssistantConversation>().AsNoTracking().SingleOrDefaultAsync(item => item.Id == conversationId && item.ConsultationId == consultationId, cancellationToken);
        if (conversation is null) return null;
        var messages = await (from message in dbContext.Set<ConsultationAssistantMessage>().AsNoTracking()
                              join citation in dbContext.Set<ConsultationAssistantMessageCitation>().AsNoTracking() on message.Id equals citation.MessageId into citations
                              where message.ConversationId == conversationId
                              orderby message.CreatedAt, message.Id
                              select new { Message = message, Citations = citations }).ToArrayAsync(cancellationToken);
        var citationIds = messages.SelectMany(item => item.Citations.Select(citation => citation.DocumentPassageId)).Distinct().ToArray();
        var evidence = await knowledge.GetPassagesAsync(consultationId, citationIds, cancellationToken);
        return new ConsultationAssistantConversationDetails(
            new ConsultationAssistantConversationSummary(conversation.Id, conversation.Title, conversation.CreatedAt, conversation.UpdatedAt, conversation.ArchivedAt),
            messages.Select(item => ToMessage(item.Message, item.Citations.Select(citation => evidence.GetValueOrDefault(citation.DocumentPassageId)).Where(item => item is not null).Cast<ConsultationEvidence>().ToArray())).ToArray());
    }

    public async Task<bool> RenameAsync(Guid consultationId, Guid conversationId, string title, CancellationToken cancellationToken)
    {
        var conversation = await dbContext.Set<ConsultationAssistantConversation>().SingleOrDefaultAsync(item => item.Id == conversationId && item.ConsultationId == consultationId, cancellationToken);
        if (conversation is null) return false;
        conversation.Rename(title, timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> ArchiveAsync(Guid consultationId, Guid conversationId, CancellationToken cancellationToken)
    {
        var conversation = await dbContext.Set<ConsultationAssistantConversation>().SingleOrDefaultAsync(item => item.Id == conversationId && item.ConsultationId == consultationId, cancellationToken);
        if (conversation is null) return false;
        conversation.Archive(timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async IAsyncEnumerable<ConsultationAssistantStreamEvent> SendAsync(Guid consultationId, Guid conversationId, string content, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var item in run.SendAsync(consultationId, conversationId, content, cancellationToken)) yield return item;
    }

    public async IAsyncEnumerable<ConsultationAssistantStreamEvent> RetryAsync(Guid consultationId, Guid conversationId, Guid userMessageId, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var item in run.RetryAsync(consultationId, conversationId, userMessageId, cancellationToken)) yield return item;
    }

    private static ConsultationAssistantMessageResult ToMessage(ConsultationAssistantMessage message, ConsultationEvidence[] evidence) => new(message.Id, message.Role, message.Content, message.Status, message.ErrorMessage, message.CreatedAt, message.CompletedAt, message.FailedAt, message.ProviderName, message.Model, evidence, DeserializeDetails(message.StructuredContent));

    private static ConsultationAssistantAnswerDetails? DeserializeDetails(string? value)
    {
        if (value is null) return null;
        var details = JsonSerializer.Deserialize<ConsultationAssistantAnswerDetails>(value);
        return details is null ? null : new ConsultationAssistantAnswerDetails(details.Status ?? "InsufficientEvidence", details.Gaps ?? [], details.FollowUps ?? [], details.ReasoningSummary);
    }
}
