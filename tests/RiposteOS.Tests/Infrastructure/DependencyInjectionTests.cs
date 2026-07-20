using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RiposteOS.Infrastructure;
using RiposteOS.Infrastructure.Ai;
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

        var sources = scope.ServiceProvider.GetServices<IOpportunitySource>().ToArray();
        Assert.Collection(
            sources,
            source => Assert.IsType<BoampSource>(source),
            source => Assert.IsType<TedSource>(source),
            source => Assert.IsType<PlaceSource>(source));
        Assert.NotNull(scope.ServiceProvider.GetService<SourcingSettingsStore>());
        Assert.NotNull(scope.ServiceProvider.GetService<AiProviderHealthCheckJob>());
        Assert.NotNull(scope.ServiceProvider.GetService<AiExecutionPayloadRetentionJob>());
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

    [Theory]
    [InlineData("Ted:BaseUrl", "relative")]
    [InlineData("Ted:InitialLookbackDays", "-1")]
    [InlineData("Ted:InitialLookbackDays", "366")]
    [InlineData("Ted:OverlapDays", "-1")]
    [InlineData("Ted:OverlapDays", "31")]
    public void InvalidTedOptionsFailValidation(string key, string value)
    {
        var services = new ServiceCollection();
        services.AddInfrastructure(Configuration(new Dictionary<string, string?> { [key] = value }));
        using var provider = services.BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(() =>
            _ = provider.GetRequiredService<IOptions<TedOptions>>().Value);
    }

    [Theory]
    [InlineData("Place:BaseUrl", "relative")]
    [InlineData("Place:InitialLookbackDays", "-1")]
    [InlineData("Place:InitialLookbackDays", "366")]
    [InlineData("Place:OverlapDays", "-1")]
    [InlineData("Place:OverlapDays", "31")]
    [InlineData("Place:RequestDelayMilliseconds", "-1")]
    [InlineData("Place:RequestDelayMilliseconds", "5001")]
    public void InvalidPlaceOptionsFailValidation(string key, string value)
    {
        var services = new ServiceCollection();
        services.AddInfrastructure(Configuration(new Dictionary<string, string?> { [key] = value }));
        using var provider = services.BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(() =>
            _ = provider.GetRequiredService<IOptions<PlaceOptions>>().Value);
    }

    [Theory]
    [InlineData("SourcingSynchronization:Cron", "")]
    [InlineData("SourcingSynchronization:Cron", "not-a-cron")]
    [InlineData("SourcingSynchronization:SuccessSlaHours", "0")]
    [InlineData("SourcingSynchronization:SuccessSlaHours", "169")]
    public void InvalidSynchronizationOptionsFailValidation(string key, string value)
    {
        var services = new ServiceCollection();
        services.AddInfrastructure(Configuration(new Dictionary<string, string?> { [key] = value }));
        using var provider = services.BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(() =>
            _ = provider.GetRequiredService<IOptions<SourcingSynchronizationOptions>>().Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-cron")]
    public void InvalidAiProviderHealthCheckCronFailsValidation(string cron)
    {
        var services = new ServiceCollection();
        services.AddInfrastructure(Configuration(new Dictionary<string, string?>
        {
            ["AiProviderHealthCheck:Cron"] = cron,
        }));
        using var provider = services.BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(() =>
            _ = provider.GetRequiredService<IOptions<AiProviderHealthCheckOptions>>().Value);
    }

    [Theory]
    [InlineData("not-a-cron", "30")]
    [InlineData("0 3 * * *", "0")]
    [InlineData("0 3 * * *", "366")]
    public void InvalidAiExecutionPayloadRetentionOptionsFailValidation(string cron, string retentionDays)
    {
        var services = new ServiceCollection();
        services.AddInfrastructure(Configuration(new Dictionary<string, string?>
        {
            ["AiExecutionPayloadRetention:Cron"] = cron,
            ["AiExecutionPayloadRetention:RetentionDays"] = retentionDays,
        }));
        using var provider = services.BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(() =>
            _ = provider.GetRequiredService<IOptions<AiExecutionPayloadRetentionOptions>>().Value);
    }

    private static IConfiguration Configuration(Dictionary<string, string?>? overrides = null)
    {
        var values = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Database"] = "Host=localhost;Database=riposteos;Username=test;Password=test",
            ["Boamp:BaseUrl"] = "https://boamp.example/api/",
            ["Boamp:InitialLookbackDays"] = "30",
            ["Boamp:OverlapDays"] = "2",
            ["Ted:BaseUrl"] = "https://ted.example/",
            ["Ted:InitialLookbackDays"] = "30",
            ["Ted:OverlapDays"] = "2",
            ["Place:BaseUrl"] = "https://place.example/",
            ["Place:InitialLookbackDays"] = "30",
            ["Place:OverlapDays"] = "2",
            ["Place:RequestDelayMilliseconds"] = "0",
            ["SourcingSynchronization:Cron"] = "0 * * * *",
            ["SourcingSynchronization:SuccessSlaHours"] = "25",
            ["AiProviderHealthCheck:Cron"] = "*/5 * * * *",
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
