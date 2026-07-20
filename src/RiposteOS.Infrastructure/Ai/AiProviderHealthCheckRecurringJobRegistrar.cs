using Hangfire;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace RiposteOS.Infrastructure.Ai;

public sealed class AiProviderHealthCheckRecurringJobRegistrar(
    IRecurringJobManager recurringJobs,
    IOptions<AiProviderHealthCheckOptions> options) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        recurringJobs.AddOrUpdate<AiProviderHealthCheckJob>(
            "ai-provider-health-check",
            job => job.ExecuteAsync(CancellationToken.None),
            options.Value.Cron,
            new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
