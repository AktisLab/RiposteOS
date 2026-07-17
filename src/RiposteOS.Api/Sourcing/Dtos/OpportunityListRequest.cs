namespace RiposteOS.Api.Sourcing.Dtos;

public sealed class OpportunityListRequest
{
    public int? Page { get; init; }

    public int? PageSize { get; init; }

    public string? Filter { get; init; }

    public string? OrderBy { get; init; }

    public string[]? Departments { get; init; }

    public string? Cpv { get; init; }
}
