using System.Security.Cryptography;
using System.Text;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiposteOS.Core.Ai;
using RiposteOS.Core.Documents;
using RiposteOS.Infrastructure.Ai;
using RiposteOS.Infrastructure.Ai.Execution;
using RiposteOS.Infrastructure.Ai.Tasks;
using RiposteOS.Infrastructure.Persistence;

namespace RiposteOS.Infrastructure.Documents;

public sealed class DocumentEmbeddingJob(
    RiposteDbContext dbContext,
    IAiEmbeddingTaskResolver embeddingResolver,
    AiExecutionRecorder executionRecorder,
    TimeProvider timeProvider,
    ILogger<DocumentEmbeddingJob> logger)
{
    private static readonly Action<ILogger, Guid, Exception?> LogFailed = LoggerMessage.Define<Guid>(LogLevel.Error, new EventId(1, nameof(LogFailed)), "Document embedding {DocumentProcessingRunId} failed");

    [AutomaticRetry(Attempts = 0)]
    [DisableConcurrentExecution(600)]
    public async Task ExecuteAsync(Guid documentProcessingRunId, CancellationToken cancellationToken)
    {
        var passages = await (from run in dbContext.Set<DocumentProcessingRun>()
                              join passage in dbContext.Set<DocumentPassage>() on run.Id equals passage.DocumentProcessingRunId
                              join document in dbContext.Set<StoredDocument>().AsNoTracking() on run.StoredDocumentId equals document.Id
                              where run.Id == documentProcessingRunId && run.Status == DocumentProcessingStatus.Completed
                              orderby passage.Ordinal
                              select new { Passage = passage, document.Id, document.OriginalFileName }).ToArrayAsync(cancellationToken);
        if (passages.Length == 0) return;

        var now = timeProvider.GetUtcNow();
        var existing = await dbContext.Set<DocumentPassageEmbedding>().Where(item => passages.Select(passage => passage.Passage.Id).Contains(item.DocumentPassageId)).ToDictionaryAsync(item => item.DocumentPassageId, cancellationToken);
        AiEmbeddingTaskClient? client;
        try
        {
            client = await embeddingResolver.ResolveAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            LogFailed(logger, documentProcessingRunId, exception);
            foreach (var passage in passages)
            {
                var embedding = GetOrCreate(passage.Passage, "Non configuré", "Non configuré", existing, now);
                embedding.Fail("L'indexation des passages a échoué. Réessayez.", now);
            }
            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }
        if (client is null)
        {
            foreach (var passage in passages)
            {
                var embedding = GetOrCreate(passage.Passage, "Non configuré", "Non configuré", existing, now);
                embedding.Fail("L'indexation IA n'est pas configurée.", now);
            }
            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        using var execution = await executionRecorder.StartScopeAsync(
            new AiExecutionStart(
                AiExecutionOperation.DocumentEmbedding,
                new AiExecutionSubject(AiExecutionSubjectKind.Document, passages[0].Id, passages[0].OriginalFileName),
                documentProcessingRunId,
                client.ProviderName,
                client.Model,
                client.ProviderId),
            cancellationToken);
        try
        {
            foreach (var item in passages)
            {
                var hash = Hash(item.Passage.Text);
                var embedding = GetOrCreate(item.Passage, client.ProviderName, client.Model, existing, now);
                if (embedding.Matches(hash, client.ProviderName, client.Model)) continue;
                if (!embedding.TryStart(timeProvider.GetUtcNow())) continue;
                var generated = await client.Generator.GenerateAsync([item.Passage.Text], cancellationToken: cancellationToken);
                embedding.Complete(generated[0].Vector.ToArray(), timeProvider.GetUtcNow());
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            await execution.CompleteAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            foreach (var embedding in existing.Values.Where(item => item.Status == DocumentPassageEmbeddingStatus.Running)) embedding.Fail("L'indexation a été interrompue. Réessayez.", timeProvider.GetUtcNow());
            await dbContext.SaveChangesAsync(CancellationToken.None);
            await execution.FailAsync("L'indexation a été interrompue. Réessayez.", false, CancellationToken.None);
            throw;
        }
        catch (Exception exception)
        {
            LogFailed(logger, documentProcessingRunId, exception);
            foreach (var embedding in existing.Values.Where(item => item.Status == DocumentPassageEmbeddingStatus.Running)) embedding.Fail("L'indexation des passages a échoué. Réessayez.", timeProvider.GetUtcNow());
            await dbContext.SaveChangesAsync(CancellationToken.None);
            await execution.FailAsync("L'indexation des passages a échoué. Réessayez.", false, CancellationToken.None);
        }
    }

    private DocumentPassageEmbedding GetOrCreate(DocumentPassage passage, string providerName, string model, Dictionary<Guid, DocumentPassageEmbedding> existing, DateTimeOffset now)
    {
        var hash = Hash(passage.Text);
        if (existing.TryGetValue(passage.Id, out var embedding))
        {
            if (!embedding.Matches(hash, providerName, model))
            {
                dbContext.Remove(embedding);
                embedding = new DocumentPassageEmbedding(passage.Id, hash, providerName, model, now);
                dbContext.Add(embedding);
                existing[passage.Id] = embedding;
            }
            return embedding;
        }
        embedding = new DocumentPassageEmbedding(passage.Id, hash, providerName, model, now);
        dbContext.Add(embedding);
        existing.Add(passage.Id, embedding);
        return embedding;
    }

    private static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}
