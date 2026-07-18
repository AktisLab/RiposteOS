namespace RiposteOS.Api.Consultations.Dtos;

public sealed record ConsultationDocumentResponse(
    Guid Id,
    string OriginalFileName,
    string ContentType,
    long Size,
    DateTimeOffset CreatedAt,
    string Kind,
    DateTimeOffset AddedAt,
    string DownloadUrl);
