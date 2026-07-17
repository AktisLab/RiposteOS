namespace RiposteOS.Api.Sourcing.Dtos;

public sealed record ImportRunListResponse(
    ImportRunResponse[] Items,
    int TotalCount,
    int Page,
    int PageSize);
