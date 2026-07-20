namespace RiposteOS.Api.Ai.Dtos;

public sealed record AiExecutionLogDetailsResponse(
    AiExecutionLogResponse Execution,
    string? Input,
    string? Output);
