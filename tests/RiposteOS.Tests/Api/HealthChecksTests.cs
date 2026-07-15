using Microsoft.AspNetCore.Mvc.Testing;

namespace RiposteOS.Tests.Api;

public sealed class HealthChecksTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public HealthChecksTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task LiveHealthCheckIsAvailable()
    {
        using var response = await _client.GetAsync("/health/live", CancellationToken.None);

        response.EnsureSuccessStatusCode();
    }
}
