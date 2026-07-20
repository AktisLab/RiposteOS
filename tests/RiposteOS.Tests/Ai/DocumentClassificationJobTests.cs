using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using RiposteOS.Core.Ai;
using RiposteOS.Core.Consultations;
using RiposteOS.Core.Documents;
using RiposteOS.Infrastructure.Ai;
using RiposteOS.Infrastructure.Persistence;

namespace RiposteOS.Tests.Ai;

public sealed class DocumentClassificationJobTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 19, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task MissingClassificationIsIgnored()
    {
        await using var dbContext = CreateDbContext();
        var job = CreateJob(dbContext, new NoClientResolver());

        await job.ExecuteAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Empty(dbContext.Set<ConsultationDocumentClassification>());
    }

    [Fact]
    public async Task UnconfiguredTaskIsRecordedWithoutCallingAProvider()
    {
        await using var dbContext = CreateDbContext();
        var consultationId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var classification = new ConsultationDocumentClassification(consultationId, documentId, Now);
        dbContext.Add(classification);
        dbContext.Add(new ConsultationDocument(consultationId, documentId, ConsultationDocumentKind.Other, ConsultationDocumentKindOrigin.Automatic, Now));
        await dbContext.SaveChangesAsync();

        await CreateJob(dbContext, new NoClientResolver()).ExecuteAsync(classification.Id, CancellationToken.None);

        Assert.Equal(DocumentClassificationStatus.NotConfigured, classification.Status);
        Assert.Equal("Le classement IA n'est pas configuré.", classification.ErrorMessage);
    }

    [Fact]
    public async Task MissingPassagesProducesRetryableFailure()
    {
        await using var dbContext = CreateDbContext();
        var consultationId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var classification = new ConsultationDocumentClassification(consultationId, documentId, Now);
        dbContext.Add(classification);
        dbContext.Add(new ConsultationDocument(consultationId, documentId, ConsultationDocumentKind.Other, ConsultationDocumentKindOrigin.Automatic, Now));
        dbContext.Add(StoredDocument(documentId));
        dbContext.Add(CompletedRun(documentId));
        await dbContext.SaveChangesAsync();

        await CreateJob(dbContext, new ResolvedClientResolver()).ExecuteAsync(classification.Id, CancellationToken.None);

        Assert.Equal(DocumentClassificationStatus.Failed, classification.Status);
        Assert.Equal("Le document n'est pas encore analysé.", classification.ErrorMessage);
    }

    [Fact]
    public async Task ValidResponseCompletesClassificationWithProviderSnapshotAndEvidence()
    {
        await using var dbContext = CreateDbContext();
        var consultationId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var classification = new ConsultationDocumentClassification(consultationId, documentId, Now);
        var link = new ConsultationDocument(consultationId, documentId, ConsultationDocumentKind.Other, ConsultationDocumentKindOrigin.Automatic, Now);
        var run = CompletedRun(documentId);
        dbContext.AddRange(classification, link, StoredDocument(documentId), run);
        await dbContext.SaveChangesAsync();
        var passage = new DocumentPassage(run.Id, 1, "Prix et bordereau", 1, "Prix", null);
        var duplicateOrdinalPassage = new DocumentPassage(run.Id, 1, "Conditions financières", 1, "Montants", null);
        dbContext.AddRange(passage, duplicateOrdinalPassage);
        await dbContext.SaveChangesAsync();
        var client = new ResponseClient("{\"kind\":\"Pricing\",\"confidence\":\"High\",\"evidenceOrdinals\":[2]}");
        var logger = new CapturingLogger();

        var timeProvider = new FixedTimeProvider(Now.AddMinutes(1));
        await new DocumentClassificationJob(dbContext, new ResolvedClientResolver(client), new AiExecutionRecorder(dbContext, timeProvider), timeProvider, logger).ExecuteAsync(classification.Id, CancellationToken.None);

        Assert.Null(logger.Exception);
        Assert.Equal(DocumentClassificationStatus.Completed, classification.Status);
        Assert.Equal(ConsultationDocumentKind.Pricing, classification.ProposedKind);
        Assert.Equal(DocumentClassificationConfidence.High, classification.Confidence);
        Assert.Equal([(new[] { passage, duplicateOrdinalPassage }).OrderBy(item => item.Id).ElementAt(1).Id], classification.EvidencePassageIds);
        Assert.Equal("provider", classification.ProviderName);
        Assert.Equal("model", classification.Model);
        Assert.Equal(ConsultationDocumentKind.Pricing, link.Kind);
        var execution = await dbContext.Set<AiExecutionLog>().SingleAsync();
        Assert.Equal(AiExecutionStatus.Completed, execution.Status);
        Assert.Equal("provider", execution.ProviderName);
        Assert.Equal("model", execution.Model);
        var payload = await dbContext.Set<AiExecutionPayload>().SingleAsync();
        Assert.Contains("Prix et bordereau", payload.Input);
        using var output = JsonDocument.Parse(payload.Output!);
        Assert.Contains("Pricing", output.RootElement.GetProperty("text").GetString());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingLinkOrManualCorrectionNeverCallsTheClassifier(bool manualLink)
    {
        await using var dbContext = CreateDbContext();
        var consultationId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var classification = new ConsultationDocumentClassification(consultationId, documentId, Now);
        dbContext.Add(classification);
        if (manualLink)
        {
            dbContext.Add(new ConsultationDocument(consultationId, documentId, ConsultationDocumentKind.Pricing, Now));
        }

        await dbContext.SaveChangesAsync();
        var resolver = new CountingResolver();

        await CreateJob(dbContext, resolver).ExecuteAsync(classification.Id, CancellationToken.None);

        Assert.Equal(0, resolver.CallCount);
        Assert.Equal(DocumentClassificationStatus.Queued, classification.Status);
    }

    [Fact]
    public async Task InvalidProviderResponseIsRecordedAsSafeFailure()
    {
        await using var dbContext = CreateDbContext();
        var consultationId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var classification = new ConsultationDocumentClassification(consultationId, documentId, Now);
        var run = CompletedRun(documentId);
        dbContext.AddRange(
            classification,
            new ConsultationDocument(consultationId, documentId, ConsultationDocumentKind.Other, ConsultationDocumentKindOrigin.Automatic, Now),
            StoredDocument(documentId),
            run);
        await dbContext.SaveChangesAsync();
        dbContext.Add(new DocumentPassage(run.Id, 1, "Contenu", 1, null, null));
        await dbContext.SaveChangesAsync();

        await CreateJob(dbContext, new ResolvedClientResolver(new ResponseClient("not-json"))).ExecuteAsync(classification.Id, CancellationToken.None);

        Assert.Equal(DocumentClassificationStatus.Failed, classification.Status);
        Assert.Equal("Le classement IA a échoué. Réessayez.", classification.ErrorMessage);
    }

    [Theory]
    [InlineData("{\"kind\":\"Unknown\",\"confidence\":\"High\",\"evidenceOrdinals\":[1]}")]
    [InlineData("{\"kind\":\"Pricing\",\"confidence\":\"Unknown\",\"evidenceOrdinals\":[1]}")]
    [InlineData("{\"kind\":\"Pricing\",\"confidence\":\"High\",\"evidenceOrdinals\":[]}")]
    [InlineData("{\"kind\":\"Pricing\",\"confidence\":\"High\",\"evidenceOrdinals\":[1,1,1,1]}")]
    [InlineData("{\"kind\":\"Pricing\",\"confidence\":\"High\",\"evidenceOrdinals\":[99]}")]
    public async Task SemanticallyInvalidProviderResponseIsRecordedAsSafeFailure(string response)
    {
        await using var dbContext = CreateDbContext();
        var consultationId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var classification = new ConsultationDocumentClassification(consultationId, documentId, Now);
        var run = CompletedRun(documentId);
        dbContext.AddRange(classification, new ConsultationDocument(consultationId, documentId, ConsultationDocumentKind.Other, ConsultationDocumentKindOrigin.Automatic, Now), StoredDocument(documentId), run);
        await dbContext.SaveChangesAsync();
        dbContext.Add(new DocumentPassage(run.Id, 1, "Contenu", 1, null, null));
        await dbContext.SaveChangesAsync();

        await CreateJob(dbContext, new ResolvedClientResolver(new ResponseClient(response))).ExecuteAsync(classification.Id, CancellationToken.None);

        Assert.Equal(DocumentClassificationStatus.Failed, classification.Status);
        Assert.Equal("Le classement IA a échoué. Réessayez.", classification.ErrorMessage);
    }

    [Fact]
    public async Task AlreadyFinishedClassificationIsIgnored()
    {
        await using var dbContext = CreateDbContext();
        var classification = new ConsultationDocumentClassification(Guid.NewGuid(), Guid.NewGuid(), Now);
        classification.Fail("failed", Now);
        dbContext.Add(classification);
        await dbContext.SaveChangesAsync();
        var resolver = new CountingResolver();

        await CreateJob(dbContext, resolver).ExecuteAsync(classification.Id, CancellationToken.None);

        Assert.Equal(DocumentClassificationStatus.Failed, classification.Status);
        Assert.Equal(0, resolver.CallCount);
    }

    private static DocumentProcessingRun CompletedRun(Guid documentId)
    {
        var run = new DocumentProcessingRun(documentId, Now);
        run.TryStart(Now);
        run.Complete(1, 0, Now);
        return run;
    }

    private static StoredDocument StoredDocument(Guid documentId) =>
        new(documentId, "document.pdf", "application/pdf", 1, new string('a', 64), Now);

    private static DocumentClassificationJob CreateJob(RiposteDbContext dbContext, IAiTaskClientResolver resolver)
    {
        var timeProvider = new FixedTimeProvider(Now.AddMinutes(1));
        return new(dbContext, resolver, new AiExecutionRecorder(dbContext, timeProvider), timeProvider, NullLogger<DocumentClassificationJob>.Instance);
    }

    private static RiposteDbContext CreateDbContext() => new(new DbContextOptionsBuilder<RiposteDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .Options);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class NoClientResolver : IAiTaskClientResolver
    {
        public Task<AiTaskClient?> ResolveAsync(AiTask task, CancellationToken cancellationToken) => Task.FromResult<AiTaskClient?>(null);
    }

    private sealed class ResolvedClientResolver(IChatClient? client = null) : IAiTaskClientResolver
    {
        public Task<AiTaskClient?> ResolveAsync(AiTask task, CancellationToken cancellationToken) =>
            Task.FromResult<AiTaskClient?>(new AiTaskClient(client!, Guid.NewGuid(), "provider", "model"));
    }

    private sealed class CountingResolver : IAiTaskClientResolver
    {
        public int CallCount { get; private set; }
        public Task<AiTaskClient?> ResolveAsync(AiTask task, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult<AiTaskClient?>(null);
        }
    }

    private sealed class ResponseClient(string response) : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, response)));

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }

    private sealed class CapturingLogger : Microsoft.Extensions.Logging.ILogger<DocumentClassificationJob>
    {
        public Exception? Exception { get; private set; }
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;
        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) => Exception = exception;
    }
}
