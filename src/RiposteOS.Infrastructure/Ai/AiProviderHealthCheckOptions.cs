namespace RiposteOS.Infrastructure.Ai;

public sealed class AiProviderHealthCheckOptions
{
    public const string SectionName = "AiProviderHealthCheck";
    public string Cron { get; init; } = "*/5 * * * *";
}
