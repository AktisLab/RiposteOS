namespace RiposteOS.Api.Ai.Dtos;

public sealed record AiExecutionLogListResponse(
    AiExecutionLogResponse[] Items,
    int TotalCount,
    int Page,
    int PageSize);
