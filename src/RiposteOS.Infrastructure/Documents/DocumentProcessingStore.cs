using Microsoft.EntityFrameworkCore;
using RiposteOS.Core.Documents;
using RiposteOS.Infrastructure.Persistence;

namespace RiposteOS.Infrastructure.Documents;

public sealed class DocumentProcessingStore(
    RiposteDbContext dbContext,
    TimeProvider timeProvider)
{
    public static bool IsSupported(string contentType) => contentType is
        "application/pdf"
        or "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
        or "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public async Task<DocumentProcessingQueueResult> QueueAsync(
        Guid storedDocumentId,
        CancellationToken cancellationToken)
    {
        var run = await dbContext.Set<DocumentProcessingRun>().SingleOrDefaultAsync(
            item => item.StoredDocumentId == storedDocumentId,
            cancellationToken);
        if (run is null)
        {
            run = new DocumentProcessingRun(storedDocumentId, timeProvider.GetUtcNow());
            dbContext.Set<DocumentProcessingRun>().Add(run);
            return new DocumentProcessingQueueResult(run, true);
        }

        if (run.Status == DocumentProcessingStatus.Failed)
        {
            run.Retry(timeProvider.GetUtcNow());
            return new DocumentProcessingQueueResult(run, true);
        }

        return new DocumentProcessingQueueResult(run, false);
    }

    public async Task FailAsync(Guid runId, string message, CancellationToken cancellationToken)
    {
        var run = await dbContext.Set<DocumentProcessingRun>().SingleOrDefaultAsync(
            item => item.Id == runId,
            cancellationToken);
        if (run is not null && run.Status is DocumentProcessingStatus.Queued or DocumentProcessingStatus.Running)
        {
            run.Fail(message, timeProvider.GetUtcNow());
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<bool> StartAsync(Guid runId, CancellationToken cancellationToken)
    {
        var startedAt = timeProvider.GetUtcNow();
        if (string.Equals(
                dbContext.Database.ProviderName,
                "Microsoft.EntityFrameworkCore.InMemory",
                StringComparison.Ordinal))
        {
            var run = await dbContext.Set<DocumentProcessingRun>().SingleOrDefaultAsync(
                item => item.Id == runId,
                cancellationToken);
            if (run is null || !run.TryStart(startedAt))
            {
                return false;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }

        dbContext.ChangeTracker.Clear();
        return await dbContext.Set<DocumentProcessingRun>()
            .Where(run => run.Id == runId && run.Status == DocumentProcessingStatus.Queued)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(run => run.Status, DocumentProcessingStatus.Running)
                .SetProperty(run => run.StartedAt, startedAt), cancellationToken) == 1;
    }
}

public sealed record DocumentProcessingQueueResult(DocumentProcessingRun Run, bool Enqueue);
