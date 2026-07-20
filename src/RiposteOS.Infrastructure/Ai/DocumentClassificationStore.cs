using Microsoft.EntityFrameworkCore;
using RiposteOS.Core.Ai;
using RiposteOS.Infrastructure.Persistence;
namespace RiposteOS.Infrastructure.Ai;

public sealed class DocumentClassificationStore(RiposteDbContext dbContext, TimeProvider timeProvider)
{
    public async Task<(ConsultationDocumentClassification Classification, bool Enqueue)> QueueAsync(Guid consultationId, Guid documentId, CancellationToken ct)
    {
        var item = await dbContext.Set<ConsultationDocumentClassification>().SingleOrDefaultAsync(x => x.ConsultationId == consultationId && x.StoredDocumentId == documentId, ct);
        if (item is null) { item = new ConsultationDocumentClassification(consultationId, documentId, timeProvider.GetUtcNow()); dbContext.Set<ConsultationDocumentClassification>().Add(item); return (item, true); }
        if (item.Status is DocumentClassificationStatus.Failed or DocumentClassificationStatus.NotConfigured) { item.Retry(timeProvider.GetUtcNow()); return (item, true); }
        return (item, false);
    }
}
