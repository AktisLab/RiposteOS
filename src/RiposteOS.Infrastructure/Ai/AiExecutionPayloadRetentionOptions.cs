namespace RiposteOS.Infrastructure.Ai;

public sealed class AiExecutionPayloadRetentionOptions
{
    public const string SectionName = "AiExecutionPayloadRetention";
    public string Cron { get; init; } = "0 3 * * *";
    public int RetentionDays { get; init; } = 30;
}
