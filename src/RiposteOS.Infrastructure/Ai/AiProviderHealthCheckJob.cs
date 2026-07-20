using Hangfire;

namespace RiposteOS.Infrastructure.Ai;

public sealed class AiProviderHealthCheckJob(AiFacade facade)
{
    [AutomaticRetry(Attempts = 0)]
    [DisableConcurrentExecution(300)]
    public Task ExecuteAsync(CancellationToken cancellationToken) =>
        facade.RefreshEnabledProviderHealthAsync(cancellationToken);
}
