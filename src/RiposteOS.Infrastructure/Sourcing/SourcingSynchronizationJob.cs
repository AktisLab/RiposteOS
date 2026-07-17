using Hangfire;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace RiposteOS.Infrastructure.Sourcing;

public sealed class SourcingSynchronizationJob(
    SourcingFacade sourcingFacade,
    ImportRunStore runStore,
    TimeProvider timeProvider,
    IOptions<SourcingSynchronizationOptions> options,
    ILogger<SourcingSynchronizationJob> logger)
{
    private static readonly Action<ILogger, string, int, DateTimeOffset?, Exception?> LogSlaExceeded =
        LoggerMessage.Define<string, int, DateTimeOffset?>(
            LogLevel.Warning,
            new EventId(1, nameof(LogSlaExceeded)),
            "{Source} synchronization exceeded its {SuccessSlaHours}-hour SLA. Last successful run: {LastSuccessfulAt}");

    [AutomaticRetry(Attempts = 3)]
    public async Task ExecuteAsync(string source, CancellationToken cancellationToken)
    {
        await runStore.ReconcileAsync(source, cancellationToken);
        var lastSuccessfulAt = await runStore.GetLastSuccessfulAtAsync(source, cancellationToken);
        if (lastSuccessfulAt is null
            || lastSuccessfulAt < timeProvider.GetUtcNow().AddHours(-options.Value.SuccessSlaHours))
        {
            LogSlaExceeded(logger, source, options.Value.SuccessSlaHours, lastSuccessfulAt, null);
        }

        await sourcingFacade.QueueImportAsync(source, cancellationToken);
    }
}
