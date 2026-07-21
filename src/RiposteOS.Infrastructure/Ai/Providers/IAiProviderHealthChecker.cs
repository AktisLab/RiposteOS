using RiposteOS.Core.Ai;

namespace RiposteOS.Infrastructure.Ai.Providers;

public interface IAiProviderHealthChecker
{
    Task<AiProviderHealthStatus> CheckAsync(AiProvider provider, CancellationToken cancellationToken);
    Task<AiProviderHealthStatus> TestAsync(AiProvider provider, CancellationToken cancellationToken) => CheckAsync(provider, cancellationToken);
}
