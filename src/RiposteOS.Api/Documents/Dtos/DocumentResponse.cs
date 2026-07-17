namespace RiposteOS.Api.Documents.Dtos;

public sealed record DocumentResponse(
    Guid Id,
    string OriginalFileName,
    string ContentType,
    long Size,
    DateTimeOffset CreatedAt);
