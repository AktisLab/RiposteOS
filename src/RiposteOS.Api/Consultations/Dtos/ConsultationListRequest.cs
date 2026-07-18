namespace RiposteOS.Api.Consultations.Dtos;

public sealed class ConsultationListRequest
{
    public int? Page { get; init; }

    public int? PageSize { get; init; }

    public string? Filter { get; init; }

    public string? OrderBy { get; init; }
}
