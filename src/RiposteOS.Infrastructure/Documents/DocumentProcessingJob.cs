using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiposteOS.Core.Documents;
using RiposteOS.Infrastructure.Persistence;

namespace RiposteOS.Infrastructure.Documents;

public sealed class DocumentProcessingJob(
    RiposteDbContext dbContext,
    DocumentProcessingStore processingStore,
    IObjectStorage objectStorage,
    IDocumentParser parser,
    TimeProvider timeProvider,
    ILogger<DocumentProcessingJob> logger)
{
    private static readonly Action<ILogger, Guid, Exception?> LogProcessingFailed =
        LoggerMessage.Define<Guid>(LogLevel.Error, new EventId(1, nameof(LogProcessingFailed)),
            "Document processing {DocumentProcessingRunId} failed");

    [AutomaticRetry(Attempts = 0)]
    public async Task ExecuteAsync(Guid runId, CancellationToken cancellationToken)
    {
        if (!await processingStore.StartAsync(runId, cancellationToken))
        {
            return;
        }

        try
        {
            var document = await (
                from processingRun in dbContext.Set<DocumentProcessingRun>().AsNoTracking()
                join storedDocument in dbContext.Set<StoredDocument>().AsNoTracking()
                    on processingRun.StoredDocumentId equals storedDocument.Id
                where processingRun.Id == runId
                select storedDocument).SingleAsync(cancellationToken);
            await using var content = await objectStorage.OpenReadAsync(document.StorageKey, cancellationToken);
            var parsed = await parser.ParseAsync(
                document.OriginalFileName,
                document.ContentType,
                content,
                cancellationToken);

            await using var transaction = dbContext.Database.IsRelational()
                ? await dbContext.Database.BeginTransactionAsync(cancellationToken)
                : null;
            var run = await dbContext.Set<DocumentProcessingRun>().SingleAsync(item => item.Id == runId, cancellationToken);
            dbContext.Set<DocumentPassage>().AddRange(parsed.Passages.Select((passage, index) => new DocumentPassage(
                runId,
                index + 1,
                passage.Text,
                passage.PageNumber,
                passage.SectionTitle,
                passage.SourceLocation)));
            run.Complete(parsed.PageCount, parsed.Passages.Count, timeProvider.GetUtcNow());
            await dbContext.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await processingStore.FailAsync(runId, "L'analyse a été interrompue. Réessayez.", CancellationToken.None);
            throw;
        }
        catch (Exception exception)
        {
            LogProcessingFailed(logger, runId, exception);
            await processingStore.FailAsync(runId, "L'analyse du document a échoué. Réessayez.", CancellationToken.None);
            throw;
        }
    }
}
