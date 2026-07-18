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
            document.AddedAt,
            $"/api/documents/{document.Id}/content");

    public static ConsultationDocumentResponse[] ToDocumentResponses(
        IEnumerable<ConsultationDocumentResult> documents) =>
        documents.Select(ToDocumentResponse).ToArray();
}
