namespace RiposteOS.Api.Sourcing.Dtos;

public sealed record OpportunityListResponse(
    OpportunityListItem[] Items,
    int TotalCount,
    int Page,
    int PageSize);
