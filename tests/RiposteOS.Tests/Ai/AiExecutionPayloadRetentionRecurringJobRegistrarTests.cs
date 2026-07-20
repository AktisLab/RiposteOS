using Hangfire;
using Microsoft.Extensions.Options;
using RiposteOS.Infrastructure.Ai;
using RiposteOS.Tests.TestSupport;

namespace RiposteOS.Tests.Ai;

public sealed class AiExecutionPayloadRetentionRecurringJobRegistrarTests
{
    [Fact]
    public async Task RegistersOneUtcPayloadRetentionJob()
    {
        var recurringJobs = new RecordingRecurringJobManager();
        var registrar = new AiExecutionPayloadRetentionRecurringJobRegistrar(
            recurringJobs,
            Options.Create(new AiExecutionPayloadRetentionOptions { Cron = "0 3 * * *" }));

        await registrar.StartAsync(CancellationToken.None);

        Assert.Equal(
            [("ai-execution-payload-retention", "0 3 * * *", TimeZoneInfo.Utc)],
            recurringJobs.Jobs);
    }
}
