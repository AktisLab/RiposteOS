using Microsoft.EntityFrameworkCore;
using RiposteOS.Core.Consultations;
using RiposteOS.Core.Documents;
using RiposteOS.Infrastructure.Persistence;

namespace RiposteOS.Infrastructure.Consultations.Knowledge;

public sealed class ConsultationKnowledgeFacade(
    RiposteDbContext dbContext,
    ConsultationRetrievalService retrieval)
{
    private const int ContextPassageLimit = 3;
    private const int DocumentLimit = 40;
    private const int OutlineLimit = 30;
    private const int SectionPassageLimit = 12;

    public Task<ConsultationRetrievalResult> SearchAsync(
        Guid consultationId,
        string query,
        CancellationToken cancellationToken) =>
        retrieval.RetrieveAsync(consultationId, query, cancellationToken);

    public async Task<ConsultationEvidence[]> GetPassageContextAsync(
        Guid consultationId,
        Guid passageId,
        CancellationToken cancellationToken)
    {
        var target = await (from link in dbContext.Set<ConsultationDocument>().AsNoTracking()
                            join run in dbContext.Set<DocumentProcessingRun>().AsNoTracking() on link.StoredDocumentId equals run.StoredDocumentId
                            join passage in dbContext.Set<DocumentPassage>().AsNoTracking() on run.Id equals passage.DocumentProcessingRunId
                            where link.ConsultationId == consultationId && passage.Id == passageId
                            select new { passage.DocumentProcessingRunId, passage.Ordinal }).SingleOrDefaultAsync(cancellationToken);
        if (target is null) return [];

        return await (from link in dbContext.Set<ConsultationDocument>().AsNoTracking()
                      join document in dbContext.Set<StoredDocument>().AsNoTracking() on link.StoredDocumentId equals document.Id
                      join run in dbContext.Set<DocumentProcessingRun>().AsNoTracking() on document.Id equals run.StoredDocumentId
                      join passage in dbContext.Set<DocumentPassage>().AsNoTracking() on run.Id equals passage.DocumentProcessingRunId
                      where link.ConsultationId == consultationId
                            && passage.DocumentProcessingRunId == target.DocumentProcessingRunId
                            && passage.Ordinal >= target.Ordinal - 1
                            && passage.Ordinal <= target.Ordinal + 1
                      orderby passage.Ordinal
                      select new ConsultationEvidence(passage.Id, 0, document.Id, document.OriginalFileName, passage.PageNumber, passage.SectionTitle, passage.Ordinal, passage.Text))
            .Take(ContextPassageLimit)
            .ToArrayAsync(cancellationToken);
    }

    public Task<ConsultationKnowledgeDocument[]> ListDocumentsAsync(
        Guid consultationId,
        CancellationToken cancellationToken) =>
        (from link in dbContext.Set<ConsultationDocument>().AsNoTracking()
         join document in dbContext.Set<StoredDocument>().AsNoTracking() on link.StoredDocumentId equals document.Id
         join run in dbContext.Set<DocumentProcessingRun>().AsNoTracking() on document.Id equals run.StoredDocumentId into runs
         where link.ConsultationId == consultationId
         orderby document.OriginalFileName, document.Id
         select new ConsultationKnowledgeDocument(document.Id, document.OriginalFileName, link.Kind.ToString(), runs.Any(item => item.Status == DocumentProcessingStatus.Completed)))
        .Take(DocumentLimit)
        .ToArrayAsync(cancellationToken);

    public Task<ConsultationKnowledgeSection[]> GetDocumentOutlineAsync(
        Guid consultationId,
        Guid documentId,
        CancellationToken cancellationToken) =>
        (from link in dbContext.Set<ConsultationDocument>().AsNoTracking()
         join run in dbContext.Set<DocumentProcessingRun>().AsNoTracking() on link.StoredDocumentId equals run.StoredDocumentId
         join passage in dbContext.Set<DocumentPassage>().AsNoTracking() on run.Id equals passage.DocumentProcessingRunId
         where link.ConsultationId == consultationId && link.StoredDocumentId == documentId
         orderby passage.PageNumber, passage.Ordinal
         select new ConsultationKnowledgeSection(passage.PageNumber, passage.SectionTitle))
        .Distinct()
        .Take(OutlineLimit)
        .ToArrayAsync(cancellationToken);

    public Task<ConsultationEvidence[]> GetDocumentSectionAsync(
        Guid consultationId,
        Guid documentId,
        string sectionTitle,
        CancellationToken cancellationToken) =>
        (from link in dbContext.Set<ConsultationDocument>().AsNoTracking()
         join document in dbContext.Set<StoredDocument>().AsNoTracking() on link.StoredDocumentId equals document.Id
         join run in dbContext.Set<DocumentProcessingRun>().AsNoTracking() on document.Id equals run.StoredDocumentId
         join passage in dbContext.Set<DocumentPassage>().AsNoTracking() on run.Id equals passage.DocumentProcessingRunId
         where link.ConsultationId == consultationId && document.Id == documentId && passage.SectionTitle == sectionTitle
         orderby passage.Ordinal
         select new ConsultationEvidence(passage.Id, 0, document.Id, document.OriginalFileName, passage.PageNumber, passage.SectionTitle, passage.Ordinal, passage.Text))
        .Distinct()
        .Take(SectionPassageLimit)
        .ToArrayAsync(cancellationToken);

    public async Task<Dictionary<Guid, ConsultationEvidence>> GetPassagesAsync(
        Guid consultationId,
        IReadOnlyCollection<Guid> passageIds,
        CancellationToken cancellationToken)
    {
        if (passageIds.Count == 0) return [];

        return await (from link in dbContext.Set<ConsultationDocument>().AsNoTracking()
                      join document in dbContext.Set<StoredDocument>().AsNoTracking() on link.StoredDocumentId equals document.Id
                      join run in dbContext.Set<DocumentProcessingRun>().AsNoTracking() on document.Id equals run.StoredDocumentId
                      join passage in dbContext.Set<DocumentPassage>().AsNoTracking() on run.Id equals passage.DocumentProcessingRunId
                      where link.ConsultationId == consultationId && passageIds.Contains(passage.Id)
                      select new ConsultationEvidence(passage.Id, 0, document.Id, document.OriginalFileName, passage.PageNumber, passage.SectionTitle, passage.Ordinal, passage.Text))
            .ToDictionaryAsync(item => item.PassageId, cancellationToken);
    }
}
