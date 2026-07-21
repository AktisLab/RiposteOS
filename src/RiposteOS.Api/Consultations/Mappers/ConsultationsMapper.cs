using Riok.Mapperly.Abstractions;
using RiposteOS.Api.Consultations.Dtos;
using RiposteOS.Infrastructure.Consultations;

namespace RiposteOS.Api.Consultations.Mappers;

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public static partial class ConsultationsMapper
{
    public static partial ConsultationResponse ToConsultationResponse(
        ConsultationResult consultation);

    public static partial ConsultationResponse[] ToConsultationResponses(
        IEnumerable<ConsultationResult> consultations);

    public static ConsultationDocumentResponse ToDocumentResponse(
        ConsultationDocumentResult document) =>
        new(
            document.Id,
            document.OriginalFileName,
            document.ContentType,
            document.Size,
            document.CreatedAt,
            document.Kind.ToString(),
            document.KindOrigin.ToString(),
            document.AddedAt,
            $"/api/documents/{document.Id}/content",
            new DocumentAnalysisResponse(
                document.Analysis.Status,
                document.Analysis.QueuedAt,
                document.Analysis.StartedAt,
                document.Analysis.CompletedAt,
                document.Analysis.FailedAt,
                document.Analysis.PageCount,
                document.Analysis.PassageCount,
                document.Analysis.ErrorMessage),
            new DocumentClassificationResponse(
                document.Classification.Status,
                document.Classification.ProposedKind,
                document.Classification.Confidence,
                document.Classification.QueuedAt,
                document.Classification.StartedAt,
                document.Classification.CompletedAt,
                document.Classification.FailedAt,
                document.Classification.ProviderName,
                document.Classification.Model,
                document.Classification.ErrorMessage),
            new DocumentEmbeddingResponse(
                document.Embedding.Status,
                document.Embedding.IndexedPassageCount,
                document.Embedding.PassageCount,
                document.Embedding.ErrorMessage));

    public static ConsultationDocumentResponse[] ToDocumentResponses(
        IEnumerable<ConsultationDocumentResult> documents) =>
        documents.Select(ToDocumentResponse).ToArray();

    public static DocumentAnalysisPassageResponse[] ToDocumentAnalysisPassageResponses(
        IEnumerable<DocumentPassageResult> passages) =>
        passages.Select(passage => new DocumentAnalysisPassageResponse(
            passage.Ordinal,
            passage.Text,
            passage.PageNumber,
            passage.SectionTitle,
            passage.SourceLocation)).ToArray();
}
