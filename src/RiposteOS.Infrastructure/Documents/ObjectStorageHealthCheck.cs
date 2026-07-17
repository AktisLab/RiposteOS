using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace RiposteOS.Infrastructure.Documents;

public sealed class ObjectStorageHealthCheck(IObjectStorage objectStorage) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        await objectStorage.CanAccessAsync(cancellationToken)
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("Le stockage objet est indisponible.");
}
