namespace RiposteOS.Api.Documents.Dtos;

public sealed record DocumentListResponse(DocumentResponse[] Items, int TotalCount, int Page, int PageSize);
