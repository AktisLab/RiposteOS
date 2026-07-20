using Hangfire;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace RiposteOS.Infrastructure.Ai;

public sealed class AiExecutionPayloadRetentionRecurringJobRegistrar(
    IRecurringJobManager recurringJobs,
    IOptions<AiExecutionPayloadRetentionOptions> options) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        recurringJobs.AddOrUpdate<AiExecutionPayloadRetentionJob>(
            "ai-execution-payload-retention",
            job => job.ExecuteAsync(CancellationToken.None),
            options.Value.Cron,
            new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
