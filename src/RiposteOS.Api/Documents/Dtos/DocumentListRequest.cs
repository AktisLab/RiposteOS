namespace RiposteOS.Api.Documents.Dtos;

public sealed class DocumentListRequest
{
    public int? Page { get; init; }

    public int? PageSize { get; init; }

    public string? Filter { get; init; }

    public string? OrderBy { get; init; }
}
