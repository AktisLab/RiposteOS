using Cronos;
using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using RiposteOS.Core.Sourcing;

namespace RiposteOS.Infrastructure.Sourcing;

public sealed class SourcingRecurringJobRegistrar(
    IRecurringJobManager recurringJobs,
    IServiceScopeFactory scopeFactory,
    IOptions<SourcingSynchronizationOptions> options) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var settings = await scope.ServiceProvider
            .GetRequiredService<SourcingSettingsStore>()
            .GetAsync(cancellationToken);
        Register(
            recurringJobs,
            scope.ServiceProvider.GetServices<IOpportunitySource>(),
            settings,
            options.Value.Cron);
    }

    public static bool IsValidCron(string? cron) =>
        !string.IsNullOrWhiteSpace(cron)
        && cron.Length <= 100
        && CronExpression.TryParse(cron, out _);

    public static void Register(
        IRecurringJobManager recurringJobs,
        IEnumerable<IOpportunitySource> sources,
        SourcingSettings? settings,
        string defaultCron)
    {
        foreach (var source in sources)
        {
            var sourceKey = source.Key;
            var cron = settings is null
                ? defaultCron
                : sourceKey switch
                {
                    SourcingSource.Boamp => settings.BoampCron,
                    SourcingSource.Ted => settings.TedCron,
                    SourcingSource.Place => settings.PlaceCron,
                    _ => throw new InvalidOperationException($"Unsupported sourcing source '{sourceKey}'."),
                };
            recurringJobs.AddOrUpdate<SourcingSynchronizationJob>(
                $"sourcing-sync-{sourceKey.ToLowerInvariant()}",
                job => job.ExecuteAsync(sourceKey, CancellationToken.None),
                cron,
                new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
