using RiposteOS.Core.Ai;

namespace RiposteOS.Infrastructure.Ai;

public interface IAiProviderHealthChecker
{
    Task<AiProviderHealthStatus> CheckAsync(AiProvider provider, CancellationToken cancellationToken);
}
