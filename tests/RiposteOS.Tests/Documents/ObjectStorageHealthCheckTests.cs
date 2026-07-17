using Microsoft.Extensions.Diagnostics.HealthChecks;
using RiposteOS.Infrastructure.Documents;
using RiposteOS.Tests.TestSupport;

namespace RiposteOS.Tests.Documents;

public sealed class ObjectStorageHealthCheckTests
{
    [Fact]
    public async Task ReportsHealthyWhenStorageIsAvailable()
    {
        var check = new ObjectStorageHealthCheck(new TestObjectStorage());

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task ReportsUnhealthyWhenStorageIsUnavailable()
    {
        var storage = new TestObjectStorage { IsAvailable = false };
        var check = new ObjectStorageHealthCheck(storage);

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }
}
