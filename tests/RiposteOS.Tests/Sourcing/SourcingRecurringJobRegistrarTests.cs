using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RiposteOS.Core.Sourcing;
using RiposteOS.Infrastructure.Persistence;
using RiposteOS.Infrastructure.Sourcing;
using RiposteOS.Tests.TestSupport;

namespace RiposteOS.Tests.Sourcing;

public sealed class SourcingRecurringJobRegistrarTests
{
    [Fact]
    public async Task RegistersOneUtcRecurringJobPerSource()
    {
        var services = new ServiceCollection();
        var databaseName = Guid.NewGuid().ToString();
        services.AddDbContext<RiposteDbContext>(options =>
            options.UseInMemoryDatabase(databaseName));
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<SourcingSettingsStore>();
        services.AddSingleton<IOpportunitySource>(new EmptySource(SourcingSource.Boamp));
        services.AddSingleton<IOpportunitySource>(new EmptySource(SourcingSource.Ted));
        using var provider = services.BuildServiceProvider();
        var recurringJobs = new RecordingRecurringJobManager();
        var registrar = new SourcingRecurringJobRegistrar(
            recurringJobs,
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new SourcingSynchronizationOptions { Cron = "15 * * * *" }));

        await registrar.StartAsync(CancellationToken.None);
        await registrar.StopAsync(CancellationToken.None);

        Assert.Collection(
            recurringJobs.Jobs.OrderBy(job => job.Id),
            job => Assert.Equal(("sourcing-sync-boamp", "15 * * * *", TimeZoneInfo.Utc), job),
            job => Assert.Equal(("sourcing-sync-ted", "15 * * * *", TimeZoneInfo.Utc), job));
    }

    [Fact]
    public async Task RegistersPersistedCronPerSource()
    {
        var services = new ServiceCollection();
        var databaseName = Guid.NewGuid().ToString();
        services.AddDbContext<RiposteDbContext>(options =>
            options.UseInMemoryDatabase(databaseName));
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<SourcingSettingsStore>();
        services.AddSingleton<IOpportunitySource>(new EmptySource(SourcingSource.Boamp));
        services.AddSingleton<IOpportunitySource>(new EmptySource(SourcingSource.Ted));
        using var provider = services.BuildServiceProvider();
        await using (var scope = provider.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<SourcingSettingsStore>().UpdateAsync(
                TestSourcingProfiles.Create(boampCron: "5 * * * *", tedCron: "0 */6 * * *"),
                CancellationToken.None);
        }

        var recurringJobs = new RecordingRecurringJobManager();
        var registrar = new SourcingRecurringJobRegistrar(
            recurringJobs,
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new SourcingSynchronizationOptions { Cron = "15 * * * *" }));

        await registrar.StartAsync(CancellationToken.None);

        Assert.Collection(
            recurringJobs.Jobs.OrderBy(job => job.Id),
            job => Assert.Equal(("sourcing-sync-boamp", "5 * * * *", TimeZoneInfo.Utc), job),
            job => Assert.Equal(("sourcing-sync-ted", "0 */6 * * *", TimeZoneInfo.Utc), job));
    }

    [Theory]
    [InlineData("0 * * * *", true)]
    [InlineData("0 6,18 * * *", true)]
    [InlineData("", false)]
    [InlineData("not-a-cron", false)]
    [InlineData("0 0 0 0 0 0", false)]
    public void ValidatesStandardCronExpressions(string cron, bool expected)
    {
        Assert.Equal(expected, SourcingRecurringJobRegistrar.IsValidCron(cron));
    }

    [Fact]
    public void RejectsAnUnsupportedSourceWhenRegisteringSchedules()
    {
        var settings = new SourcingSettings(
            TestSourcingProfiles.Create(),
            DateTimeOffset.UtcNow);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            SourcingRecurringJobRegistrar.Register(
                new RecordingRecurringJobManager(),
                [new EmptySource("UNKNOWN")],
                settings,
                SourcingSettings.DefaultSynchronizationCron));

        Assert.Contains("UNKNOWN", exception.Message, StringComparison.Ordinal);
    }

    private sealed class EmptySource(string key) : IOpportunitySource
    {
        public string Key { get; } = key;

        public DateOnly GetStartDate(DateOnly today, DateOnly? lastSuccessfulDate) => today;

        public IAsyncEnumerable<SourcingPage> ReadPagesAsync(
            SourcingSettings settings,
            DateOnly startDate,
            DateOnly endDate,
            CancellationToken cancellationToken) => AsyncEnumerable.Empty<SourcingPage>();
    }

}
