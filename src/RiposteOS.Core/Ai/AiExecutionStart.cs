namespace RiposteOS.Core.Ai;

public sealed record AiExecutionStart(
    AiExecutionOperation Operation,
    AiExecutionSubject Subject,
    Guid? CorrelationId,
    string? ProviderName,
    string? Model,
    Guid? ProviderId);
