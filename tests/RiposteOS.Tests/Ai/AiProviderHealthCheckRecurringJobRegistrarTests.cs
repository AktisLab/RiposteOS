using Hangfire;
using Microsoft.Extensions.Options;
using RiposteOS.Infrastructure.Ai;
using RiposteOS.Tests.TestSupport;

namespace RiposteOS.Tests.Ai;

public sealed class AiProviderHealthCheckRecurringJobRegistrarTests
{
    [Fact]
    public async Task RegistersOneUtcRecurringHealthCheck()
    {
        var recurringJobs = new RecordingRecurringJobManager();
        var registrar = new AiProviderHealthCheckRecurringJobRegistrar(
            recurringJobs,
            Options.Create(new AiProviderHealthCheckOptions { Cron = "*/5 * * * *" }));

        await registrar.StartAsync(CancellationToken.None);
        await registrar.StopAsync(CancellationToken.None);

        Assert.Equal(
            [("ai-provider-health-check", "*/5 * * * *", TimeZoneInfo.Utc)],
            recurringJobs.Jobs);
    }
}
