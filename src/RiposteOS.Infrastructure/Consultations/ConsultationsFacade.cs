using Hangfire;
using Gridify;
using Gridify.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using RiposteOS.Core.Consultations;
using RiposteOS.Core.Documents;
using RiposteOS.Core.Sourcing;
using RiposteOS.Infrastructure.Persistence;
using RiposteOS.Infrastructure.Documents;

namespace RiposteOS.Infrastructure.Consultations;

public sealed class ConsultationsFacade(
    RiposteDbContext dbContext,
    TimeProvider timeProvider,
    DocumentProcessingStore processingStore,
    IBackgroundJobClient jobClient)
{
    private static readonly ConsultationGridifyMapper ConsultationMapper = new();

    public async Task<ConsultationPromotionResult> PromoteOpportunityAsync(
        Guid opportunityId,
        CancellationToken cancellationToken)
    {
        await using var transaction = dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;
        var opportunity = await GetOpportunityForUpdateAsync(opportunityId, cancellationToken);
        if (opportunity is null)
        {
            return new ConsultationPromotionResult(null, false);
        }

        var consultation = await dbContext.Set<Consultation>()
            .SingleOrDefaultAsync(item => item.OpportunityId == opportunityId, cancellationToken);
        var created = consultation is null;
        if (created)
        {
            consultation = Consultation.FromOpportunity(opportunity, timeProvider.GetUtcNow());
            dbContext.Set<Consultation>().Add(consultation);
        }

        opportunity.Retain();
        await dbContext.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return new ConsultationPromotionResult(
            ToResult(consultation!, opportunity.Source, opportunity.SourceId),
            created);
    }

    public async Task<ConsultationResult> CreateAsync(
        string title,
        string buyer,
        DateTimeOffset? responseDeadline,
        string? noticeUrl,
        CancellationToken cancellationToken)
    {
        var consultation = new Consultation(
            title,
            buyer,
            responseDeadline,
            noticeUrl,
            timeProvider.GetUtcNow());
        dbContext.Set<Consultation>().Add(consultation);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToResult(consultation, null, null);
    }

    public async Task<ConsultationPageResult> ListAsync(
        int page,
        int pageSize,
        string? filter,
        string? orderBy,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(pageSize, 100);

        var gridifyQuery = new GridifyQuery(
            page,
            pageSize,
            filter,
            string.IsNullOrWhiteSpace(orderBy) ? "responseDeadline,id" : $"{orderBy},id");
        if (!gridifyQuery.IsValid(ConsultationMapper))
        {
            return new ConsultationPageResult([], 0, ["Le filtre ou le tri demandé est invalide."]);
        }

        var result = await Query().GridifyAsync(
            gridifyQuery,
            cancellationToken,
            ConsultationMapper);
        return new ConsultationPageResult(result.Data.ToArray(), result.Count, []);
    }

    public Task<ConsultationResult?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        Query().SingleOrDefaultAsync(consultation => consultation.Id == id, cancellationToken);

    public async Task<ConsultationDocumentResult[]?> ListDocumentsAsync(
        Guid consultationId,
        CancellationToken cancellationToken)
    {
        if (!await dbContext.Set<Consultation>()
            .AsNoTracking()
            .AnyAsync(consultation => consultation.Id == consultationId, cancellationToken))
        {
            return null;
        }

        var documents = await (
            from link in dbContext.Set<ConsultationDocument>().AsNoTracking()
            join document in dbContext.Set<StoredDocument>().AsNoTracking()
                on link.StoredDocumentId equals document.Id
            join run in dbContext.Set<DocumentProcessingRun>().AsNoTracking()
                on document.Id equals run.StoredDocumentId into runs
            from run in runs.DefaultIfEmpty()
            where link.ConsultationId == consultationId
            orderby link.AddedAt descending, document.Id
            select new
            {
                document.Id,
                document.OriginalFileName,
                document.ContentType,
                document.Size,
                document.CreatedAt,
                link.Kind,
                link.AddedAt,
                Run = run,
            }).ToArrayAsync(cancellationToken);

        return documents.Select(document => new ConsultationDocumentResult(
            document.Id,
            document.OriginalFileName,
            document.ContentType,
            document.Size,
            document.CreatedAt,
            document.Kind,
            document.AddedAt,
            ToAnalysis(document.ContentType, document.Run))).ToArray();
    }

    public async Task<DocumentPassageResult[]?> ListDocumentPassagesAsync(
        Guid consultationId,
        Guid storedDocumentId,
        CancellationToken cancellationToken)
    {
        var run = await (
            from link in dbContext.Set<ConsultationDocument>().AsNoTracking()
            join processingRun in dbContext.Set<DocumentProcessingRun>().AsNoTracking()
                on link.StoredDocumentId equals processingRun.StoredDocumentId into runs
            from processingRun in runs.DefaultIfEmpty()
            where link.ConsultationId == consultationId
                && link.StoredDocumentId == storedDocumentId
            select processingRun).SingleOrDefaultAsync(cancellationToken);

        if (run is null)
        {
            var documentExists = await dbContext.Set<ConsultationDocument>().AsNoTracking()
                .AnyAsync(
                    link => link.ConsultationId == consultationId
                        && link.StoredDocumentId == storedDocumentId,
                    cancellationToken);
            return documentExists ? [] : null;
        }

        return await dbContext.Set<DocumentPassage>().AsNoTracking()
            .Where(passage => passage.DocumentProcessingRunId == run.Id)
            .OrderBy(passage => passage.Ordinal)
            .Select(passage => new DocumentPassageResult(
                passage.Ordinal,
                passage.Text,
                passage.PageNumber,
                passage.SectionTitle,
                passage.SourceLocation))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<ConsultationDocumentAttachmentResult> AttachDocumentAsync(
        Guid consultationId,
        Guid storedDocumentId,
        ConsultationDocumentKind kind,
        CancellationToken cancellationToken)
    {
        if (!await dbContext.Set<Consultation>()
            .AsNoTracking()
            .AnyAsync(consultation => consultation.Id == consultationId, cancellationToken))
        {
            return new ConsultationDocumentAttachmentResult(
                ConsultationDocumentAttachmentStatus.ConsultationNotFound,
                null);
        }

        await using var transaction = dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;
        var document = await GetStoredDocumentForUpdateAsync(storedDocumentId, cancellationToken);
        if (document is null)
        {
            return new ConsultationDocumentAttachmentResult(
                ConsultationDocumentAttachmentStatus.StoredDocumentNotFound,
                null);
        }

        var link = await dbContext.Set<ConsultationDocument>().SingleOrDefaultAsync(item =>
            item.ConsultationId == consultationId && item.StoredDocumentId == storedDocumentId,
            cancellationToken);
        var created = link is null;
        if (created)
        {
            dbContext.Set<ConsultationDocument>().Add(new ConsultationDocument(
                consultationId,
                storedDocumentId,
                kind,
                timeProvider.GetUtcNow()));
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        DocumentProcessingQueueResult? processing = null;
        if (DocumentProcessingStore.IsSupported(document.ContentType))
        {
            processing = await processingStore.QueueAsync(document.Id, cancellationToken);
            if (processing.Enqueue)
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        if (processing is { Enqueue: true })
        {
            await EnqueueAsync(processing.Run.Id);
        }

        return new ConsultationDocumentAttachmentResult(
            created ? ConsultationDocumentAttachmentStatus.Created : ConsultationDocumentAttachmentStatus.Existing,
            await GetDocumentAsync(consultationId, storedDocumentId, cancellationToken));
    }

    public async Task<ConsultationDocumentProcessingResult> QueueDocumentProcessingAsync(
        Guid consultationId,
        Guid storedDocumentId,
        CancellationToken cancellationToken)
    {
        if (!await dbContext.Set<ConsultationDocument>().AsNoTracking().AnyAsync(item =>
                item.ConsultationId == consultationId && item.StoredDocumentId == storedDocumentId,
                cancellationToken))
        {
            return new ConsultationDocumentProcessingResult(ConsultationDocumentProcessingStatus.DocumentNotFound, null);
        }

        await using var transaction = dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;
        var document = await GetStoredDocumentForUpdateAsync(storedDocumentId, cancellationToken);
        if (document is null)
        {
            return new ConsultationDocumentProcessingResult(ConsultationDocumentProcessingStatus.DocumentNotFound, null);
        }

        if (!DocumentProcessingStore.IsSupported(document.ContentType))
        {
            return new ConsultationDocumentProcessingResult(
                ConsultationDocumentProcessingStatus.NotSupported,
                await GetDocumentAsync(consultationId, storedDocumentId, cancellationToken));
        }

        var processing = await processingStore.QueueAsync(document.Id, cancellationToken);
        if (processing.Enqueue)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        if (processing.Enqueue)
        {
            await EnqueueAsync(processing.Run.Id);
        }

        return new ConsultationDocumentProcessingResult(
            processing.Enqueue ? ConsultationDocumentProcessingStatus.Queued : ConsultationDocumentProcessingStatus.Existing,
            await GetDocumentAsync(consultationId, storedDocumentId, cancellationToken));
    }

    public async Task<ConsultationDocumentResult?> ChangeDocumentKindAsync(
        Guid consultationId,
        Guid storedDocumentId,
        ConsultationDocumentKind kind,
        CancellationToken cancellationToken)
    {
        var link = await dbContext.Set<ConsultationDocument>().SingleOrDefaultAsync(
            document => document.ConsultationId == consultationId
                && document.StoredDocumentId == storedDocumentId,
            cancellationToken);
        if (link is null)
        {
            return null;
        }

        link.ChangeKind(kind);
        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetDocumentAsync(consultationId, storedDocumentId, cancellationToken);
    }

    public async Task<bool> DetachDocumentAsync(
        Guid consultationId,
        Guid storedDocumentId,
        CancellationToken cancellationToken)
    {
        var link = await dbContext.Set<ConsultationDocument>().SingleOrDefaultAsync(
            document => document.ConsultationId == consultationId
                && document.StoredDocumentId == storedDocumentId,
            cancellationToken);
        if (link is null)
        {
            return false;
        }

        dbContext.Remove(link);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private Task<Opportunity?> GetOpportunityForUpdateAsync(
        Guid opportunityId,
        CancellationToken cancellationToken) =>
        dbContext.Database.IsNpgsql()
            ? dbContext.Set<Opportunity>()
                .FromSqlInterpolated($"SELECT * FROM sourcing.opportunities WHERE \"Id\" = {opportunityId} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken)
            : dbContext.Set<Opportunity>()
                .SingleOrDefaultAsync(opportunity => opportunity.Id == opportunityId, cancellationToken);

    private Task<StoredDocument?> GetStoredDocumentForUpdateAsync(
        Guid storedDocumentId,
        CancellationToken cancellationToken) =>
        dbContext.Database.IsNpgsql()
            ? dbContext.Set<StoredDocument>()
                .FromSqlInterpolated($"SELECT * FROM documents.stored_documents WHERE \"Id\" = {storedDocumentId} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken)
            : dbContext.Set<StoredDocument>().SingleOrDefaultAsync(document => document.Id == storedDocumentId, cancellationToken);

    private async Task EnqueueAsync(Guid runId)
    {
        try
        {
            jobClient.Enqueue<DocumentProcessingJob>(job => job.ExecuteAsync(runId, CancellationToken.None));
        }
        catch
        {
            await processingStore.FailAsync(runId, "L'analyse n'a pas pu être transmise au worker.", CancellationToken.None);
        }
    }

    private IQueryable<ConsultationResult> Query() =>
        from consultation in dbContext.Set<Consultation>().AsNoTracking()
        join opportunity in dbContext.Set<Opportunity>().AsNoTracking()
            on consultation.OpportunityId equals (Guid?)opportunity.Id into opportunities
        from opportunity in opportunities.DefaultIfEmpty()
        select new ConsultationResult
        {
            Id = consultation.Id,
            OpportunityId = consultation.OpportunityId,
            Title = consultation.Title,
            Buyer = consultation.Buyer,
            ResponseDeadline = consultation.ResponseDeadline,
            NoticeUrl = consultation.NoticeUrl,
            Source = opportunity == null ? null : opportunity.Source,
            SourceId = opportunity == null ? null : opportunity.SourceId,
            DocumentCount = dbContext.Set<ConsultationDocument>().Count(document =>
                document.ConsultationId == consultation.Id),
            CreatedAt = consultation.CreatedAt,
            UpdatedAt = consultation.UpdatedAt,
        };

    private async Task<ConsultationDocumentResult?> GetDocumentAsync(
        Guid consultationId,
        Guid storedDocumentId,
        CancellationToken cancellationToken)
    {
        var document = await (
            from link in dbContext.Set<ConsultationDocument>().AsNoTracking()
            join storedDocument in dbContext.Set<StoredDocument>().AsNoTracking()
                on link.StoredDocumentId equals storedDocument.Id
            join run in dbContext.Set<DocumentProcessingRun>().AsNoTracking()
                on storedDocument.Id equals run.StoredDocumentId into runs
            from run in runs.DefaultIfEmpty()
            where link.ConsultationId == consultationId
                && link.StoredDocumentId == storedDocumentId
            select new
            {
                storedDocument.Id,
                storedDocument.OriginalFileName,
                storedDocument.ContentType,
                storedDocument.Size,
                storedDocument.CreatedAt,
                link.Kind,
                link.AddedAt,
                Run = run,
            }).SingleOrDefaultAsync(cancellationToken);

        return document is null
            ? null
            : new ConsultationDocumentResult(
                document.Id,
                document.OriginalFileName,
                document.ContentType,
                document.Size,
                document.CreatedAt,
                document.Kind,
                document.AddedAt,
                ToAnalysis(document.ContentType, document.Run));
    }

    private static DocumentAnalysisResult ToAnalysis(string contentType, DocumentProcessingRun? run) =>
        run is null
            ? new DocumentAnalysisResult(
                DocumentProcessingStore.IsSupported(contentType) ? "NotStarted" : "NotSupported",
                null, null, null, null, 0, 0, null)
            : new DocumentAnalysisResult(
                run.Status.ToString(),
                run.QueuedAt,
                run.StartedAt,
                run.CompletedAt,
                run.FailedAt,
                run.PageCount,
                run.PassageCount,
                run.ErrorMessage);

    private static ConsultationResult ToResult(
        Consultation consultation,
        string? source,
        string? sourceId) =>
        new()
        {
            Id = consultation.Id,
            OpportunityId = consultation.OpportunityId,
            Title = consultation.Title,
            Buyer = consultation.Buyer,
            ResponseDeadline = consultation.ResponseDeadline,
            NoticeUrl = consultation.NoticeUrl,
            Source = source,
            SourceId = sourceId,
            DocumentCount = 0,
            CreatedAt = consultation.CreatedAt,
            UpdatedAt = consultation.UpdatedAt,
        };
}
