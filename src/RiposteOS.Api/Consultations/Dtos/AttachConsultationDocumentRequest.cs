namespace RiposteOS.Api.Consultations.Dtos;

public sealed record AttachConsultationDocumentRequest(Guid DocumentId, string? Kind);
