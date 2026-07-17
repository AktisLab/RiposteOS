namespace RiposteOS.Infrastructure.Sourcing;

public sealed class SourcingSynchronizationOptions
{
    public const string SectionName = "SourcingSynchronization";

    public string Cron { get; init; } = "0 * * * *";

    public int SuccessSlaHours { get; init; } = 25;
}
