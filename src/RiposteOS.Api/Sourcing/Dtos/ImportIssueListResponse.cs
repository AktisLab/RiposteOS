namespace RiposteOS.Api.Sourcing.Dtos;

public sealed record ImportIssueListResponse(
    IReadOnlyList<ImportIssueResponse> Items,
    int TotalCount,
    int Page,
    int PageSize);
