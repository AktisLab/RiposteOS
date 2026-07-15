using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RiposteOS.Infrastructure;
using RiposteOS.Infrastructure.Sourcing;

namespace RiposteOS.Tests.Infrastructure;

public sealed class DependencyInjectionTests
{
    [Fact]
    public void DatabaseConnectionIsRequired()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddInfrastructure(configuration));

        Assert.Contains("Connection string 'Database'", exception.Message);
    }

    [Fact]
    public void ValidConfigurationRegistersSourcingAndBackgroundProcessing()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(Configuration());
        services.AddBackgroundProcessing();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.IsType<BoampSource>(scope.ServiceProvider.GetRequiredService<IOpportunitySource>());
        Assert.NotNull(scope.ServiceProvider.GetService<SourcingSettingsStore>());
    }

    [Theory]
    [InlineData("Boamp:BaseUrl", "relative")]
    [InlineData("Boamp:InitialLookbackDays", "-1")]
    [InlineData("Boamp:InitialLookbackDays", "366")]
    [InlineData("Boamp:OverlapDays", "-1")]
    [InlineData("Boamp:OverlapDays", "31")]
    public void InvalidBoampOptionsFailValidation(string key, string value)
    {
        var services = new ServiceCollection();
        services.AddInfrastructure(Configuration(new Dictionary<string, string?> { [key] = value }));
        using var provider = services.BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(() =>
            _ = provider.GetRequiredService<IOptions<BoampOptions>>().Value);
    }

    private static IConfiguration Configuration(Dictionary<string, string?>? overrides = null)
    {
        var values = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Database"] = "Host=localhost;Database=riposteos;Username=test;Password=test",
            ["Boamp:BaseUrl"] = "https://boamp.example/api/",
            ["Boamp:InitialLookbackDays"] = "30",
            ["Boamp:OverlapDays"] = "2",
        };
        if (overrides is not null)
        {
            foreach (var (key, value) in overrides)
            {
                values[key] = value;
            }
        }

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }
}
