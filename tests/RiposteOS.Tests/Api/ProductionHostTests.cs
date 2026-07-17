using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace RiposteOS.Tests.Api;

public sealed class ProductionHostTests
{
    [Fact]
    public async Task ProductionHostDoesNotExposeDevelopmentDocumentation()
    {
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseEnvironment("Production"));
        var client = factory.CreateClient();

        using var root = await client.GetAsync("/");
        using var docs = await client.GetAsync("/docs");

        root.EnsureSuccessStatusCode();
        Assert.Equal(System.Net.HttpStatusCode.NotFound, docs.StatusCode);
    }
}
