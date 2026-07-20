using System.Diagnostics;
using System.Text.Json;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiposteOS.Core.Ai;
using RiposteOS.Core.Consultations;
using RiposteOS.Core.Documents;
using RiposteOS.Infrastructure.Ai;
using RiposteOS.Infrastructure.Persistence;

namespace RiposteOS.Infrastructure.Documents;

public sealed class DocumentProcessingJob(
    RiposteDbContext dbContext,
    DocumentProcessingStore processingStore,
    IObjectStorage objectStorage,
    IDocumentParser parser,
    DocumentClassificationStore classificationStore,
    AiExecutionRecorder executionRecorder,
    IBackgroundJobClient jobClient,
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

        Guid? executionId = null;
        using var activity = AiExecutionTelemetry.Start(AiExecutionOperation.DocumentAnalysis);
        activity?.SetTag("gen_ai.operation.name", "document_processing");
        try
        {
            var document = await (
                from processingRun in dbContext.Set<DocumentProcessingRun>().AsNoTracking()
                join storedDocument in dbContext.Set<StoredDocument>().AsNoTracking()
                    on processingRun.StoredDocumentId equals storedDocument.Id
                where processingRun.Id == runId
                select storedDocument).SingleAsync(cancellationToken);
            executionId = await executionRecorder.StartAsync(
                new AiExecutionStart(
                    AiExecutionOperation.DocumentAnalysis,
                    new AiExecutionSubject(AiExecutionSubjectKind.Document, document.Id, document.OriginalFileName),
                    runId,
                    "Docling",
                    null,
                    null),
                cancellationToken);
            activity?.SetTag("gen_ai.provider.name", "Docling");
            await executionRecorder.RecordInputAsync(
                executionId.Value,
                JsonSerializer.Serialize(new
                {
                    document.Id,
                    document.OriginalFileName,
                    document.ContentType,
                    document.Size,
                    document.Sha256,
                }),
                cancellationToken);
            await using var content = await objectStorage.OpenReadAsync(document.StorageKey, cancellationToken);
            var parsed = await parser.ParseAsync(
                document.OriginalFileName,
                document.ContentType,
                content,
                cancellationToken);
            await executionRecorder.RecordOutputAsync(
                executionId.Value,
                JsonSerializer.Serialize(parsed),
                cancellationToken);

            await using var transaction = dbContext.Database.IsRelational()
                ? await dbContext.Database.BeginTransactionAsync(cancellationToken)
                : null;
            var run = await dbContext.Set<DocumentProcessingRun>().SingleAsync(item => item.Id == runId, cancellationToken);
            dbContext.Set<DocumentPassage>().RemoveRange(await dbContext.Set<DocumentPassage>()
                .Where(passage => passage.DocumentProcessingRunId == runId)
                .ToArrayAsync(cancellationToken));
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
            await executionRecorder.CompleteAsync(executionId.Value, cancellationToken);
            var attachments = await dbContext.Set<ConsultationDocument>().AsNoTracking()
                .Where(item => item.StoredDocumentId == document.Id && item.KindOrigin == ConsultationDocumentKindOrigin.Automatic)
                .Select(item => new { item.ConsultationId, item.StoredDocumentId })
                .ToArrayAsync(cancellationToken);
            foreach (var attachment in attachments)
            {
                var queued = await classificationStore.QueueAsync(attachment.ConsultationId, attachment.StoredDocumentId, cancellationToken);
                if (queued.Enqueue)
                {
                    await dbContext.SaveChangesAsync(cancellationToken);
                    jobClient.Enqueue<DocumentClassificationJob>(job => job.ExecuteAsync(queued.Classification.Id, CancellationToken.None));
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            dbContext.ChangeTracker.Clear();
            await processingStore.FailAsync(runId, "L'analyse a été interrompue. Réessayez.", CancellationToken.None);
            if (executionId is { } id)
            {
                await executionRecorder.FailAsync(id, "L'analyse a été interrompue. Réessayez.", false, CancellationToken.None);
            }
            activity?.SetStatus(ActivityStatusCode.Error, "cancelled");
            throw;
        }
        catch (Exception exception)
        {
            LogProcessingFailed(logger, runId, exception);
            dbContext.ChangeTracker.Clear();
            await processingStore.FailAsync(runId, "L'analyse du document a échoué. Réessayez.", CancellationToken.None);
            if (executionId is { } id)
            {
                await executionRecorder.FailAsync(id, "L'analyse du document a échoué. Réessayez.", false, CancellationToken.None);
            }
            activity?.SetStatus(ActivityStatusCode.Error, "failed");
            throw;
        }
    }
}
