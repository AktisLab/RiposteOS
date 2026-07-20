namespace RiposteOS.Api.Ai.Dtos;

public sealed class AiExecutionLogListRequest
{
    public int? Page { get; init; }

    public int? PageSize { get; init; }

    public string? Filter { get; init; }

    public string? OrderBy { get; init; }
}
