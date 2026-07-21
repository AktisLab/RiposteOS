using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using RiposteOS.Core.Ai;
using RiposteOS.Core.Consultations;
using RiposteOS.Infrastructure.Ai;
using RiposteOS.Infrastructure.Ai.Execution;
using RiposteOS.Infrastructure.Ai.Runtime;
using RiposteOS.Infrastructure.Ai.Tasks;
using RiposteOS.Infrastructure.Consultations.Knowledge;
using RiposteOS.Infrastructure.Persistence;

namespace RiposteOS.Infrastructure.Consultations.Assistant;

public sealed partial class ConsultationAssistantRun(
    RiposteDbContext dbContext,
    ConsultationKnowledgeFacade knowledge,
    IAiTaskClientResolver chatResolver,
    AiExecutionRecorder executionRecorder,
    AiChatClientPipeline chatPipeline,
    TimeProvider timeProvider,
    ILogger<ConsultationAssistantRun> logger)
{
    private const string InsufficientEvidenceAnswer = "Les documents indexés ne permettent pas de répondre à cette question.";

    public async IAsyncEnumerable<ConsultationAssistantStreamEvent> SendAsync(Guid consultationId, Guid conversationId, string content, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var item in RunAsync(consultationId, conversationId, content, null, cancellationToken)) yield return item;
    }

    public async IAsyncEnumerable<ConsultationAssistantStreamEvent> RetryAsync(Guid consultationId, Guid conversationId, Guid userMessageId, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var item in RunAsync(consultationId, conversationId, null, userMessageId, cancellationToken)) yield return item;
    }

    private async IAsyncEnumerable<ConsultationAssistantStreamEvent> RunAsync(Guid consultationId, Guid conversationId, string? content, Guid? userMessageId, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var events = Channel.CreateUnbounded<ConsultationAssistantStreamEvent>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });
        var producer = ProduceAsync(consultationId, conversationId, content, userMessageId, events.Writer, cancellationToken);
        await foreach (var item in events.Reader.ReadAllAsync(CancellationToken.None)) yield return item;
        await producer;
    }

    private async Task ProduceAsync(Guid consultationId, Guid conversationId, string? content, Guid? userMessageId, ChannelWriter<ConsultationAssistantStreamEvent> events, CancellationToken cancellationToken)
    {
        ConsultationAssistantMessage? assistant = null;
        AiExecutionScope? execution = null;
        try
        {
            var conversation = await dbContext.Set<ConsultationAssistantConversation>().SingleOrDefaultAsync(item => item.Id == conversationId && item.ConsultationId == consultationId && item.ArchivedAt == null, cancellationToken);
            if (conversation is null)
            {
                await events.WriteAsync(ConsultationAssistantStreamEvent.Failed("La conversation est introuvable ou archivée."), cancellationToken);
                return;
            }

            var existingUser = userMessageId is null
                ? null
                : await dbContext.Set<ConsultationAssistantMessage>().SingleOrDefaultAsync(
                    item => item.Id == userMessageId && item.ConversationId == conversationId && item.Role == ConsultationAssistantMessageRole.User,
                    cancellationToken);
            if (userMessageId is not null && existingUser is null)
            {
                await events.WriteAsync(ConsultationAssistantStreamEvent.Failed("La question à réessayer est introuvable."), cancellationToken);
                return;
            }

            var question = existingUser?.Content ?? content;
            if (string.IsNullOrWhiteSpace(question) || question.Trim().Length > ConsultationAssistantMessage.MaximumContentLength)
            {
                await events.WriteAsync(ConsultationAssistantStreamEvent.Failed("La question est invalide."), cancellationToken);
                return;
            }

            var user = existingUser ?? ConsultationAssistantMessage.CreateUser(conversationId, question, timeProvider.GetUtcNow());
            assistant = ConsultationAssistantMessage.StartAssistant(conversationId, timeProvider.GetUtcNow());
            if (existingUser is null)
            {
                if (conversation.Title == "Nouvelle conversation" && !await dbContext.Set<ConsultationAssistantMessage>().AnyAsync(item => item.ConversationId == conversationId, cancellationToken)) conversation.Rename(ProvisionalTitle(question), timeProvider.GetUtcNow());
                dbContext.Add(user);
            }
            dbContext.Add(assistant);
            await dbContext.SaveChangesAsync(cancellationToken);
            await events.WriteAsync(ConsultationAssistantStreamEvent.Started(assistant.Id), cancellationToken);

            var chat = await chatResolver.ResolveAsync(AiTask.ConsultationChat, cancellationToken);
            if (chat is null)
            {
                await FailAsync(assistant, "L'assistant IA avec recherche documentaire n'est pas configuré.", false, CancellationToken.None);
                await events.WriteAsync(ConsultationAssistantStreamEvent.Failed("L'assistant IA avec recherche documentaire n'est pas configuré.", assistant.Id), cancellationToken);
                return;
            }

            execution = await executionRecorder.StartScopeAsync(new AiExecutionStart(AiExecutionOperation.ConsultationChat, new AiExecutionSubject(AiExecutionSubjectKind.Consultation, consultationId, conversation.Title), assistant.Id, chat.ProviderName, chat.Model, chat.ProviderId), cancellationToken);
            var references = new PassageReferenceSet();
            var knowledgeTools = new ConsultationKnowledgeTools(knowledge, consultationId, references, question, (activity, ct) => events.WriteAsync(ConsultationAssistantStreamEvent.Progress(activity), ct));
            var messages = await PromptAsync(conversationId, user.Id, question, cancellationToken);
            await execution.RecordInputAsync(JsonSerializer.Serialize(new
            {
                PromptVersion,
                Messages = messages.Select(item => new { Role = item.Role.ToString(), item.Text }),
            }), cancellationToken);

            using var client = chatPipeline.CreateForTools(chat.Client, maximumIterations: 8);
            var answer = new StringBuilder();
            var reasoning = new StringBuilder();
            await foreach (var update in client.GetStreamingResponseAsync(messages, new ChatOptions
            {
                Temperature = 0,
                Tools = knowledgeTools.Create(),
                ToolMode = ChatToolMode.RequireSpecific("search_passages"),
                AllowMultipleToolCalls = false,
                Reasoning = (chat.Capabilities & AiProviderCapabilities.Reasoning) != 0
                    ? new ReasoningOptions { Effort = ReasoningEffort.Low, Output = ReasoningOutput.Summary }
                    : null,
            }, cancellationToken))
            {
                foreach (var summary in update.Contents.OfType<TextReasoningContent>().Select(item => item.Text).Where(item => !string.IsNullOrEmpty(item)))
                {
                    reasoning.Append(summary);
                    await events.WriteAsync(ConsultationAssistantStreamEvent.ReasoningDelta(summary, assistant.Id), cancellationToken);
                }
                foreach (var text in update.Contents.OfType<TextContent>().Select(item => item.Text).Where(item => !string.IsNullOrEmpty(item)))
                {
                    answer.Append(text);
                    if (references.Count > 0) await events.WriteAsync(ConsultationAssistantStreamEvent.AnswerDelta(text, assistant.Id), cancellationToken);
                }
            }

            if (!knowledgeTools.SearchAttempted) throw new InvalidOperationException("Le provider n'a pas exécuté l'outil de recherche requis.");
            if (!knowledgeTools.IsConfigured)
            {
                await FailAsync(assistant, "L'indexation IA n'est pas configurée.", false, CancellationToken.None);
                await execution.FailAsync("L'indexation IA n'est pas configurée.", true, CancellationToken.None);
                await events.WriteAsync(ConsultationAssistantStreamEvent.Failed("L'indexation IA n'est pas configurée.", assistant.Id), cancellationToken);
                return;
            }

            if (references.Count == 0)
            {
                await CompleteAsync(assistant, chat, InsufficientEvidenceAnswer, [], "InsufficientEvidence", reasoning.ToString(), execution, events, cancellationToken);
                return;
            }

            var rawAnswer = answer.ToString().Trim();
            var validated = await ValidateOrRepairAnswerAsync(client, question, rawAnswer, references, events, cancellationToken);
            if (validated is null) throw new InvalidOperationException("La réponse ne contient pas de citations valides.");
            (rawAnswer, var cited) = validated.Value;
            var status = cited.Length == 0 ? "InsufficientEvidence" : "Answered";
            await execution.RecordOutputAsync(JsonSerializer.Serialize(new { Answer = rawAnswer }), cancellationToken);
            var displayAnswer = PassageReferenceSet.RemoveCitationMarkers(rawAnswer);
            if (cited.Length > 0) displayAnswer = string.Join('\n', displayAnswer.Split('\n').Where(line => !line.Contains(InsufficientEvidenceAnswer, StringComparison.OrdinalIgnoreCase))).Trim();
            await CompleteAsync(assistant, chat, displayAnswer, cited, status, reasoning.ToString(), execution, events, cancellationToken, recordOutput: false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (assistant is not null)
            {
                await FailAsync(assistant, "La génération a été arrêtée.", true, CancellationToken.None);
                if (execution is not null) await execution.FailAsync("La génération a été arrêtée.", false, CancellationToken.None);
                events.TryWrite(ConsultationAssistantStreamEvent.Cancelled(assistant.Id));
            }
        }
        catch (Exception exception)
        {
            GenerationFailed(logger, assistant?.Id, exception);
            if (assistant is not null)
            {
                await FailAsync(assistant, "La réponse n'a pas pu être générée. Réessayez.", false, CancellationToken.None);
                if (execution is not null) await execution.FailAsync("La réponse n'a pas pu être générée. Réessayez.", false, CancellationToken.None);
                events.TryWrite(ConsultationAssistantStreamEvent.Failed("La réponse n'a pas pu être générée. Réessayez.", assistant.Id));
            }
            else events.TryWrite(ConsultationAssistantStreamEvent.Failed("La réponse n'a pas pu être générée. Réessayez."));
        }
        finally
        {
            execution?.Dispose();
            events.TryComplete();
        }
    }

    [LoggerMessage(EventId = 2401, Level = LogLevel.Warning, Message = "Consultation assistant generation failed for message {MessageId}.")]
    private static partial void GenerationFailed(ILogger logger, Guid? messageId, Exception exception);
}
