using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using RiposteOS.Core.Ai;
using RiposteOS.Core.Consultations;
using RiposteOS.Core.Documents;
using RiposteOS.Infrastructure.Ai;
using RiposteOS.Infrastructure.Ai.Execution;
using RiposteOS.Infrastructure.Ai.Runtime;
using RiposteOS.Infrastructure.Ai.Tasks;
using RiposteOS.Infrastructure.Consultations.Assistant;
using RiposteOS.Infrastructure.Consultations.Knowledge;
using RiposteOS.Infrastructure.Consultations;
using RiposteOS.Infrastructure.Persistence;

namespace RiposteOS.Tests.Consultations;

public sealed class ConsultationAssistantFacadeTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 20, 14, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ForcesSearchThenStreamsAndPersistsValidatedCitations()
    {
        await using var dbContext = CreateDbContext();
        var (consultation, passage) = await SeedIndexedPassageAsync(dbContext, "La remise des offres est fixée au 12 septembre.");
        var chat = new ToolCallingChatClient(["**Date limite** : le 12 septembre [P1]."]);
        var facade = CreateFacade(dbContext, new FixedChatResolver(chat));
        var conversation = await facade.CreateAsync(consultation.Id, null, CancellationToken.None);

        var events = await ReadAsync(facade.SendAsync(consultation.Id, conversation!.Id, "Quelle est la date limite ?", CancellationToken.None));
        var stored = await facade.GetAsync(consultation.Id, conversation.Id, CancellationToken.None);

        Assert.True(chat.SawRequiredSearch);
        Assert.True(chat.SawFunctionResult);
        Assert.Equal(["message_started", "activity", "answer_delta", "message_completed"], events.Select(item => item.Type));
        Assert.Equal("**Date limite** : le 12 septembre.", string.Concat(events.Where(item => item.Type == "message_completed").Select(item => item.Message!.Content)));
        Assert.Equal(passage.Id, Assert.Single(stored!.Messages.Single(message => message.Role == ConsultationAssistantMessageRole.Assistant).Evidence).PassageId);
    }

    [Fact]
    public async Task CombinesProviderSearchQueriesWithTheRawUserWording()
    {
        await using var dbContext = CreateDbContext();
        var (consultation, _) = await SeedIndexedPassageAsync(dbContext, "Le marché porte sur l'évolution d'un site internet existant.");
        var embeddings = new RecordingEmbeddingResolver();
        string[] searchQueries =
        [
            "objet du marché site internet existant",
            "prestations conception développement maintenance livrables",
        ];
        var chat = new ToolCallingChatClient(
            ["Le prestataire doit faire évoluer le site existant [P1]."],
            searchQueries: searchQueries);
        var facade = CreateFacade(dbContext, new FixedChatResolver(chat), embeddings);
        var conversation = await facade.CreateAsync(consultation.Id, null, CancellationToken.None);

        await ReadAsync(facade.SendAsync(consultation.Id, conversation!.Id, "Est-ce qu'on doit fournir une application ?", CancellationToken.None));

        Assert.Equal([.. searchQueries, "Est-ce qu'on doit fournir une application ?"], embeddings.Inputs);
    }

    [Fact]
    public async Task RepairsADraftContainingAnUncitedClaimBeforePersistingIt()
    {
        await using var dbContext = CreateDbContext();
        var (consultation, passage) = await SeedIndexedPassageAsync(dbContext, "Le marché porte sur l'évolution d'un site internet existant.");
        var chat = new ToolCallingChatClient(
            ["Le marché porte sur un site internet [P1].\nLe prestataire doit aussi fournir une nouvelle application."],
            repairResponse: """{"isInsufficientEvidence":false,"statements":[{"text":"Le marché porte sur l'évolution du site internet existant.","evidenceReferences":["P1"]}]}""");
        var facade = CreateFacade(dbContext, new FixedChatResolver(chat));
        var conversation = await facade.CreateAsync(consultation.Id, null, CancellationToken.None);

        var events = await ReadAsync(facade.SendAsync(consultation.Id, conversation!.Id, "Faut-il créer une application ?", CancellationToken.None));
        var completed = Assert.IsType<ConsultationAssistantMessageResult>(events[^1].Message);

        Assert.Equal(1, chat.RepairCalls);
        Assert.Equal("message_completed", events[^1].Type);
        Assert.Equal("- Le marché porte sur l'évolution du site internet existant.", completed.Content);
        Assert.Equal(passage.Id, Assert.Single(completed.Evidence).PassageId);
        Assert.Contains(events, item => item.Activity == "Vérification de la réponse et de ses sources…");
    }

    [Fact]
    public async Task RequestsStreamsAndPersistsAReasoningSummary()
    {
        await using var dbContext = CreateDbContext();
        var (consultation, _) = await SeedIndexedPassageAsync(dbContext, "Le dossier exige un mémoire technique.");
        var chat = new ToolCallingChatClient(["Un mémoire technique est exigé [P1]."], emitReasoning: true);
        var facade = CreateFacade(dbContext, new FixedChatResolver(chat, AiProviderCapabilities.Chat | AiProviderCapabilities.ToolCalling | AiProviderCapabilities.Reasoning));
        var conversation = await facade.CreateAsync(consultation.Id, null, CancellationToken.None);

        var events = await ReadAsync(facade.SendAsync(consultation.Id, conversation!.Id, "Que faut-il remettre ?", CancellationToken.None));
        var stored = await facade.GetAsync(consultation.Id, conversation.Id, CancellationToken.None);

        Assert.True(chat.SawReasoningOptions);
        Assert.Equal("Je recherche les preuves. Je synthétise la réponse.", string.Concat(events.Where(item => item.Type == "reasoning_delta").Select(item => item.Delta)));
        Assert.Equal("Je recherche les preuves. Je synthétise la réponse.", stored!.Messages.Single(item => item.Role == ConsultationAssistantMessageRole.Assistant).Details!.ReasoningSummary);
        Assert.Contains("vision fonctionnelle globale", chat.LastInitialMessages[0].Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnswersTheMaintenanceQuestionFromTheToolEvidence()
    {
        await using var dbContext = CreateDbContext();
        var (consultation, _) = await SeedIndexedPassageAsync(dbContext, "Maintenance corrective : anomalies et incidents, environ 10 à 12 jours par an. Maintenance préventive : environ 6 jours par an. Maintenance évolutive : environ 10 jours par an.");
        var facade = CreateFacade(dbContext, new FixedChatResolver(new ToolCallingChatClient([
            "## Maintenance demandée\n\n- **Corrective** : anomalies et incidents, 10 à 12 jours par an [P1].\n- **Préventive** : environ 6 jours par an [P1].\n- **Évolutive** : environ 10 jours par an [P1].",
        ])));
        var conversation = await facade.CreateAsync(consultation.Id, null, CancellationToken.None);

        var events = await ReadAsync(facade.SendAsync(consultation.Id, conversation!.Id, "Qu'est-ce qui est demandé en terme de maintenance ?", CancellationToken.None));
        var completed = Assert.IsType<ConsultationAssistantMessageResult>(events[^1].Message);

        Assert.Equal("message_completed", events[^1].Type);
        Assert.Contains("Maintenance demandée", completed.Content, StringComparison.Ordinal);
        Assert.Single(completed.Evidence);
    }

    [Fact]
    public async Task ReusesCompletedConversationHistoryWithoutTreatingItAsEvidence()
    {
        await using var dbContext = CreateDbContext();
        var (consultation, _) = await SeedIndexedPassageAsync(dbContext, "Le délai contractuel est de trente jours.");
        var chat = new ToolCallingChatClient(["Le délai est de trente jours [P1]."]);
        var facade = CreateFacade(dbContext, new FixedChatResolver(chat));
        var conversation = await facade.CreateAsync(consultation.Id, null, CancellationToken.None);

        await ReadAsync(facade.SendAsync(consultation.Id, conversation!.Id, "Quel est le délai ?", CancellationToken.None));
        await ReadAsync(facade.SendAsync(consultation.Id, conversation.Id, "Peux-tu le rappeler ?", CancellationToken.None));

        Assert.Equal(
            [ChatRole.System, ChatRole.User, ChatRole.Assistant, ChatRole.User],
            chat.LastInitialMessages.Select(message => message.Role));
        Assert.Equal("Quel est le délai ?", chat.LastInitialMessages[1].Text);
        Assert.Equal("Le délai est de trente jours.", chat.LastInitialMessages[2].Text);
        Assert.Equal("Peux-tu le rappeler ?", chat.LastInitialMessages[3].Text);
    }

    [Fact]
    public async Task RejectsInventedOrMissingCitations()
    {
        await using var dbContext = CreateDbContext();
        var (consultation, _) = await SeedIndexedPassageAsync(dbContext, "Le dossier comporte une annexe.");
        var invented = CreateFacade(dbContext, new FixedChatResolver(new ToolCallingChatClient(["Une réponse [P99]."])));
        var first = await invented.CreateAsync(consultation.Id, null, CancellationToken.None);
        var inventedEvents = await ReadAsync(invented.SendAsync(consultation.Id, first!.Id, "Question", CancellationToken.None));
        var missing = CreateFacade(dbContext, new FixedChatResolver(new ToolCallingChatClient(["Une réponse sans preuve."])));
        var second = await missing.CreateAsync(consultation.Id, null, CancellationToken.None);
        var missingEvents = await ReadAsync(missing.SendAsync(consultation.Id, second!.Id, "Question", CancellationToken.None));

        Assert.Equal("message_failed", inventedEvents[^1].Type);
        Assert.Equal("message_failed", missingEvents[^1].Type);
    }

    [Fact]
    public async Task RetriesAFailedAnswerWithoutDuplicatingTheUserMessage()
    {
        await using var dbContext = CreateDbContext();
        var (consultation, _) = await SeedIndexedPassageAsync(dbContext, "Le marché porte sur un site internet existant.");
        var failing = CreateFacade(dbContext, new FixedChatResolver(new ToolCallingChatClient(["Réponse sans citation."])));
        var conversation = await failing.CreateAsync(consultation.Id, null, CancellationToken.None);
        await ReadAsync(failing.SendAsync(consultation.Id, conversation!.Id, "Quel produit faut-il réaliser ?", CancellationToken.None));
        var failed = await failing.GetAsync(consultation.Id, conversation.Id, CancellationToken.None);
        var user = failed!.Messages.Single(item => item.Role == ConsultationAssistantMessageRole.User);
        var retrying = CreateFacade(dbContext, new FixedChatResolver(new ToolCallingChatClient(["Il faut faire évoluer le site internet existant [P1]."])));

        var events = await ReadAsync(retrying.RetryAsync(consultation.Id, conversation.Id, user.Id, CancellationToken.None));
        var stored = await retrying.GetAsync(consultation.Id, conversation.Id, CancellationToken.None);

        Assert.Equal("message_completed", events[^1].Type);
        Assert.Single(stored!.Messages, item => item.Role == ConsultationAssistantMessageRole.User);
        Assert.Equal(2, stored.Messages.Count(item => item.Role == ConsultationAssistantMessageRole.Assistant));
    }

    [Fact]
    public async Task CompletesWithInsufficientEvidenceWhenSearchReturnsNoPassage()
    {
        await using var dbContext = CreateDbContext();
        var consultation = new Consultation("Dossier", "Acheteur", null, null, Now);
        dbContext.Add(consultation);
        await dbContext.SaveChangesAsync();
        var facade = CreateFacade(dbContext, new FixedChatResolver(new ToolCallingChatClient(["Je ne sais pas."])));
        var conversation = await facade.CreateAsync(consultation.Id, null, CancellationToken.None);

        var events = await ReadAsync(facade.SendAsync(consultation.Id, conversation!.Id, "Question", CancellationToken.None));
        var completed = Assert.IsType<ConsultationAssistantMessageResult>(events[^1].Message);

        Assert.Equal(["message_started", "activity", "message_completed"], events.Select(item => item.Type));
        Assert.Equal("Les documents indexés ne permettent pas de répondre à cette question.", completed.Content);
        Assert.Empty(completed.Evidence);
    }

    [Fact]
    public async Task AcceptsAnExplicitInsufficientEvidenceAnswerAfterSearch()
    {
        await using var dbContext = CreateDbContext();
        var (consultation, _) = await SeedIndexedPassageAsync(dbContext, "Une preuve existe mais ne répond pas à tout.");
        var facade = CreateFacade(dbContext, new FixedChatResolver(new ToolCallingChatClient(["Les documents indexés ne permettent pas de répondre à cette question."])));
        var conversation = await facade.CreateAsync(consultation.Id, null, CancellationToken.None);

        var events = await ReadAsync(facade.SendAsync(consultation.Id, conversation!.Id, "Question sans réponse", CancellationToken.None));
        var completed = Assert.IsType<ConsultationAssistantMessageResult>(events[^1].Message);

        Assert.Equal("message_completed", events[^1].Type);
        Assert.Empty(completed.Evidence);
        Assert.Equal("InsufficientEvidence", completed.Details!.Status);
    }

    [Fact]
    public async Task StreamsNativeMarkdownChunksInsteadOfPartialJson()
    {
        await using var dbContext = CreateDbContext();
        var (consultation, _) = await SeedIndexedPassageAsync(dbContext, "Une preuve existe.");
        var facade = CreateFacade(dbContext, new FixedChatResolver(new ToolCallingChatClient(["## Titre\n", "- Élément [P1]"])));
        var conversation = await facade.CreateAsync(consultation.Id, null, CancellationToken.None);

        var events = await ReadAsync(facade.SendAsync(consultation.Id, conversation!.Id, "Question", CancellationToken.None));

        Assert.Contains("## Titre\n- Élément [P1]", string.Concat(events.Where(item => item.Type == "answer_delta").Select(item => item.Delta)), StringComparison.Ordinal);
        Assert.Equal("## Titre\n- Élément", events[^1].Message!.Content);
    }

    [Fact]
    public async Task PersistsUsefulFailuresForMissingChatAndEmbeddingConfiguration()
    {
        await using var dbContext = CreateDbContext();
        var (consultation, _) = await SeedIndexedPassageAsync(dbContext, "Une preuve existe.");
        var noChat = CreateFacade(dbContext, new NoChatResolver());
        var first = await noChat.CreateAsync(consultation.Id, null, CancellationToken.None);
        var noChatEvents = await ReadAsync(noChat.SendAsync(consultation.Id, first!.Id, "Question", CancellationToken.None));
        var noEmbedding = CreateFacade(dbContext, new FixedChatResolver(new ToolCallingChatClient(["Réponse [P1]."])), new NoEmbeddingResolver());
        var second = await noEmbedding.CreateAsync(consultation.Id, null, CancellationToken.None);
        var noEmbeddingEvents = await ReadAsync(noEmbedding.SendAsync(consultation.Id, second!.Id, "Question", CancellationToken.None));

        Assert.Equal("L'assistant IA avec recherche documentaire n'est pas configuré.", noChatEvents[^1].Error);
        Assert.Equal("L'indexation IA n'est pas configurée.", noEmbeddingEvents[^1].Error);
    }

    [Fact]
    public async Task FailsWhenTheProviderIgnoresTheRequiredToolContract()
    {
        await using var dbContext = CreateDbContext();
        var (consultation, _) = await SeedIndexedPassageAsync(dbContext, "Une preuve existe.");
        var facade = CreateFacade(dbContext, new FixedChatResolver(new TextOnlyChatClient("Réponse sans tool.")));
        var conversation = await facade.CreateAsync(consultation.Id, null, CancellationToken.None);

        var events = await ReadAsync(facade.SendAsync(consultation.Id, conversation!.Id, "Question", CancellationToken.None));

        Assert.Equal(["message_started", "message_failed"], events.Select(item => item.Type));
    }

    [Fact]
    public async Task MarksThePendingAnswerCancelledWhenTheClientStopsTheRequest()
    {
        await using var dbContext = CreateDbContext();
        var (consultation, _) = await SeedIndexedPassageAsync(dbContext, "Une preuve existe.");
        var facade = CreateFacade(dbContext, new FixedChatResolver(new ToolCallingChatClient([], cancelFinalResponse: true)));
        var conversation = await facade.CreateAsync(consultation.Id, null, CancellationToken.None);
        using var cancellation = new CancellationTokenSource();
        await using var stream = facade.SendAsync(consultation.Id, conversation!.Id, "Question", cancellation.Token).GetAsyncEnumerator();

        Assert.True(await stream.MoveNextAsync());
        Assert.Equal("message_started", stream.Current.Type);
        Assert.True(await stream.MoveNextAsync());
        Assert.Equal("activity", stream.Current.Type);
        cancellation.Cancel();
        Assert.True(await stream.MoveNextAsync());
        Assert.Equal("message_cancelled", stream.Current.Type);
    }

    [Fact]
    public async Task KeepsConversationsAndEvidenceIsolatedBetweenConsultations()
    {
        await using var dbContext = CreateDbContext();
        var (first, _) = await SeedIndexedPassageAsync(dbContext, "Le premier dossier contient le délai.");
        var (second, _) = await SeedIndexedPassageAsync(dbContext, "Le second dossier contient un autre délai.");
        var facade = CreateFacade(dbContext, new FixedChatResolver(new ToolCallingChatClient(["Réponse [P1]."])));
        var conversation = await facade.CreateAsync(first.Id, "Premier", CancellationToken.None);

        Assert.Null(await facade.GetAsync(second.Id, conversation!.Id, CancellationToken.None));
        Assert.False(await facade.RenameAsync(second.Id, conversation.Id, "Interdit", CancellationToken.None));
        Assert.NotNull(await facade.CreateAsync(second.Id, "Second", CancellationToken.None));
    }

    [Fact]
    public async Task ManagesConversationsAndValidatesMessages()
    {
        await using var dbContext = CreateDbContext();
        var consultation = new Consultation("Dossier", "Acheteur", null, null, Now);
        dbContext.Add(consultation);
        await dbContext.SaveChangesAsync();
        var facade = CreateFacade(dbContext, new NoChatResolver());
        var conversation = await facade.CreateAsync(consultation.Id, "Initial", CancellationToken.None);

        Assert.NotNull(await facade.ListAsync(consultation.Id, CancellationToken.None));
        Assert.Null(await facade.ListAsync(Guid.NewGuid(), CancellationToken.None));
        Assert.True(await facade.RenameAsync(consultation.Id, conversation!.Id, "Renommée", CancellationToken.None));
        Assert.False(await facade.ArchiveAsync(consultation.Id, Guid.NewGuid(), CancellationToken.None));
        Assert.Equal("message_failed", Assert.Single(await ReadAsync(facade.SendAsync(consultation.Id, conversation.Id, " ", CancellationToken.None))).Type);
        Assert.True(await facade.ArchiveAsync(consultation.Id, conversation.Id, CancellationToken.None));
        Assert.Equal("message_failed", Assert.Single(await ReadAsync(facade.SendAsync(consultation.Id, conversation.Id, "Question", CancellationToken.None))).Type);
    }

    [Fact]
    public async Task LimitsTheAutomaticConversationTitleToEightyCharacters()
    {
        await using var dbContext = CreateDbContext();
        var (consultation, _) = await SeedIndexedPassageAsync(dbContext, "Une preuve existe.");
        var facade = CreateFacade(dbContext, new FixedChatResolver(new ToolCallingChatClient(["Réponse [P1]."])));
        var conversation = await facade.CreateAsync(consultation.Id, null, CancellationToken.None);

        await ReadAsync(facade.SendAsync(consultation.Id, conversation!.Id, new string('q', 100), CancellationToken.None));
        var stored = await facade.GetAsync(consultation.Id, conversation.Id, CancellationToken.None);

        Assert.Equal(80, stored!.Conversation.Title.Length);
        Assert.EndsWith("...", stored.Conversation.Title, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NormalizesLegacyStructuredDetailsForTheFrontend()
    {
        await using var dbContext = CreateDbContext();
        var consultation = new Consultation("Dossier", "Acheteur", null, null, Now);
        dbContext.Add(consultation);
        await dbContext.SaveChangesAsync();
        var facade = CreateFacade(dbContext, new NoChatResolver());
        var conversation = await facade.CreateAsync(consultation.Id, null, CancellationToken.None);
        var assistant = ConsultationAssistantMessage.StartAssistant(conversation!.Id, Now);
        assistant.Complete("Ancienne réponse", "Provider", "Model", "{}", Now);
        dbContext.Add(assistant);
        await dbContext.SaveChangesAsync();

        var stored = await facade.GetAsync(consultation.Id, conversation.Id, CancellationToken.None);
        var details = Assert.Single(stored!.Messages).Details;

        Assert.Equal("InsufficientEvidence", details!.Status);
        Assert.Empty(details.Gaps!);
        Assert.Empty(details.FollowUps!);
    }

    private static ConsultationAssistantFacade CreateFacade(RiposteDbContext dbContext, IAiTaskClientResolver chatResolver, IAiEmbeddingTaskResolver? embeddingResolver = null)
    {
        var timeProvider = new FixedTimeProvider(Now);
        var retrieval = new ConsultationRetrievalService(dbContext, embeddingResolver ?? new FixedEmbeddingResolver());
        var knowledge = new ConsultationKnowledgeFacade(dbContext, retrieval);
        var pipeline = new AiChatClientPipeline(NullLoggerFactory.Instance);
        var run = new ConsultationAssistantRun(dbContext, knowledge, chatResolver, new AiExecutionRecorder(dbContext, timeProvider), pipeline, timeProvider, NullLogger<ConsultationAssistantRun>.Instance);
        return new ConsultationAssistantFacade(dbContext, run, knowledge, timeProvider);
    }

    private static RiposteDbContext CreateDbContext() => new(new DbContextOptionsBuilder<RiposteDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static async Task<(Consultation Consultation, DocumentPassage Passage)> SeedIndexedPassageAsync(RiposteDbContext dbContext, string text)
    {
        var consultation = new Consultation("Dossier", "Acheteur", null, null, Now);
        var document = new StoredDocument(Guid.NewGuid(), "dce.pdf", "application/pdf", 1, new string('a', 64), Now);
        dbContext.AddRange(consultation, document);
        await dbContext.SaveChangesAsync();
        var processingRun = new DocumentProcessingRun(document.Id, Now);
        dbContext.AddRange(new ConsultationDocument(consultation.Id, document.Id, ConsultationDocumentKind.FullDce, Now), processingRun);
        await dbContext.SaveChangesAsync();
        processingRun.TryStart(Now);
        processingRun.Complete(1, 1, Now);
        var passage = new DocumentPassage(processingRun.Id, 1, text, 1, "Maintenance", null);
        dbContext.Add(passage);
        await dbContext.SaveChangesAsync();
        var embedding = new DocumentPassageEmbedding(passage.Id, new string('b', 64), "Embedding", "qwen", Now);
        embedding.TryStart(Now);
        embedding.Complete(Vector(), Now);
        dbContext.Add(embedding);
        await dbContext.SaveChangesAsync();
        return (consultation, passage);
    }

    private static async Task<List<ConsultationAssistantStreamEvent>> ReadAsync(IAsyncEnumerable<ConsultationAssistantStreamEvent> source)
    {
        var result = new List<ConsultationAssistantStreamEvent>();
        await foreach (var item in source) result.Add(item);
        return result;
    }

    private static float[] Vector()
    {
        var value = new float[DocumentPassageEmbedding.ExpectedDimension];
        value[0] = 1;
        return value;
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FixedEmbeddingResolver : IAiEmbeddingTaskResolver
    {
        public Task<AiEmbeddingTaskClient?> ResolveAsync(CancellationToken cancellationToken) => Task.FromResult<AiEmbeddingTaskClient?>(new(new FixedEmbeddingGenerator(), Guid.NewGuid(), "Embedding", "qwen"));
    }

    private sealed class RecordingEmbeddingResolver : IAiEmbeddingTaskResolver
    {
        public List<string> Inputs { get; } = [];

        public Task<AiEmbeddingTaskClient?> ResolveAsync(CancellationToken cancellationToken) =>
            Task.FromResult<AiEmbeddingTaskClient?>(new(new RecordingEmbeddingGenerator(this), Guid.NewGuid(), "Embedding", "qwen"));

        private sealed class RecordingEmbeddingGenerator(RecordingEmbeddingResolver owner) : IEmbeddingGenerator<string, Embedding<float>>
        {
            public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(IEnumerable<string> values, EmbeddingGenerationOptions? options = null, CancellationToken cancellationToken = default)
            {
                var inputs = values.ToArray();
                owner.Inputs.Add(Assert.Single(inputs));
                return Task.FromResult(new GeneratedEmbeddings<Embedding<float>>(inputs.Select(_ => new Embedding<float>(Vector()))));
            }

            public object? GetService(Type serviceType, object? serviceKey = null) => null;
            public void Dispose() { }
        }
    }

    private sealed class NoEmbeddingResolver : IAiEmbeddingTaskResolver
    {
        public Task<AiEmbeddingTaskClient?> ResolveAsync(CancellationToken cancellationToken) => Task.FromResult<AiEmbeddingTaskClient?>(null);
    }

    private sealed class FixedEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
    {
        public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(IEnumerable<string> values, EmbeddingGenerationOptions? options = null, CancellationToken cancellationToken = default) => Task.FromResult(new GeneratedEmbeddings<Embedding<float>>(values.Select(_ => new Embedding<float>(Vector()))));
        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }

    private sealed class FixedChatResolver(IChatClient client, AiProviderCapabilities capabilities = AiProviderCapabilities.Chat | AiProviderCapabilities.ToolCalling) : IAiTaskClientResolver
    {
        public Task<AiTaskClient?> ResolveAsync(AiTask task, CancellationToken cancellationToken) => Task.FromResult<AiTaskClient?>(new(client, Guid.NewGuid(), "Chat", "gpt", capabilities));
    }

    private sealed class NoChatResolver : IAiTaskClientResolver
    {
        public Task<AiTaskClient?> ResolveAsync(AiTask task, CancellationToken cancellationToken) => Task.FromResult<AiTaskClient?>(null);
    }

    private sealed class ToolCallingChatClient(
        string[] chunks,
        bool cancelFinalResponse = false,
        bool emitReasoning = false,
        string[]? searchQueries = null,
        string? repairResponse = null) : IChatClient
    {
        private int callId;
        public bool SawRequiredSearch { get; private set; }
        public bool SawFunctionResult { get; private set; }
        public bool SawReasoningOptions { get; private set; }
        public int RepairCalls { get; private set; }
        public ChatMessage[] LastInitialMessages { get; private set; } = [];

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            RepairCalls++;
            return repairResponse is null
                ? throw new NotSupportedException()
                : Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, repairResponse)));
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var messageArray = messages.ToArray();
            var hasFunctionResult = messageArray.SelectMany(message => message.Contents).OfType<FunctionResultContent>().Any();
            if (!hasFunctionResult)
            {
                LastInitialMessages = messageArray;
                SawRequiredSearch = options?.ToolMode is RequiredChatToolMode { RequiredFunctionName: "search_passages" };
                SawReasoningOptions = options?.Reasoning is { Effort: ReasoningEffort.Low, Output: ReasoningOutput.Summary };
                var queries = searchQueries ?? [messageArray.Last(message => message.Role == ChatRole.User).Text];
                var contents = new List<AIContent>();
                if (emitReasoning) contents.Add(new TextReasoningContent("Je recherche les preuves. "));
                contents.Add(new FunctionCallContent($"search-{Interlocked.Increment(ref callId)}", "search_passages", new Dictionary<string, object?> { ["queries"] = queries }));
                yield return new ChatResponseUpdate(ChatRole.Assistant, contents);
                yield break;
            }

            SawFunctionResult = true;
            if (cancelFinalResponse) await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            if (emitReasoning) yield return new ChatResponseUpdate(ChatRole.Assistant, [new TextReasoningContent("Je synthétise la réponse.")]);
            foreach (var chunk in chunks) yield return new ChatResponseUpdate(ChatRole.Assistant, chunk);
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }

    private sealed class TextOnlyChatClient(string response) : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield return new ChatResponseUpdate(ChatRole.Assistant, response);
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }
}
