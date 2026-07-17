using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RiposteOS.Core.Sourcing;
using RiposteOS.Infrastructure.Persistence;
using RiposteOS.Infrastructure.Sourcing;
using RiposteOS.Tests.TestSupport;

namespace RiposteOS.Tests.Sourcing;

public sealed class SourcingSynchronizationJobTests
{
    [Fact]
    public async Task StaleRunIsReconciledImportIsQueuedAndSlaBreachIsLogged()
    {
        var now = new DateTimeOffset(2026, 7, 16, 12, 0, 0, TimeSpan.Zero);
        await using var dbContext = CreateContext();
        var previousRun = CompletedRun(now.AddHours(-30), now.AddHours(-26));
        var staleRun = new ImportRun(SourcingSource.Boamp, now.AddHours(-3));
        dbContext.AddRange(
            previousRun,
            staleRun,
            new SourcingSettings(TestSourcingProfiles.Create(["logiciel"]), now));
        await dbContext.SaveChangesAsync();
        var timeProvider = new FixedTimeProvider(now);
        var runStore = new ImportRunStore(dbContext, timeProvider);
        var backgroundJobs = new RecordingBackgroundJobClient();
        var facade = CreateFacade(dbContext, timeProvider, runStore, backgroundJobs);
        var logger = new RecordingLogger<SourcingSynchronizationJob>();
        var job = new SourcingSynchronizationJob(
            facade,
            runStore,
            timeProvider,
            Options.Create(new SourcingSynchronizationOptions { SuccessSlaHours = 25 }),
            logger);

        await job.ExecuteAsync(SourcingSource.Boamp, CancellationToken.None);

        Assert.Equal(ImportRunStatus.Failed, staleRun.Status);
        Assert.NotNull(backgroundJobs.CreatedJob);
        Assert.Contains(logger.Messages, message => message.Contains("SLA", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RecentSuccessfulRunDoesNotRaiseAnAlert()
    {
        var now = new DateTimeOffset(2026, 7, 16, 12, 0, 0, TimeSpan.Zero);
        await using var dbContext = CreateContext();
        dbContext.Add(CompletedRun(now.AddHours(-2), now.AddHours(-1)));
        await dbContext.SaveChangesAsync();
        var timeProvider = new FixedTimeProvider(now);
        var runStore = new ImportRunStore(dbContext, timeProvider);
        var facade = CreateFacade(
            dbContext,
            timeProvider,
            runStore,
            new RecordingBackgroundJobClient());
        var logger = new RecordingLogger<SourcingSynchronizationJob>();
        var job = new SourcingSynchronizationJob(
            facade,
            runStore,
            timeProvider,
            Options.Create(new SourcingSynchronizationOptions()),
            logger);

        await job.ExecuteAsync(SourcingSource.Boamp, CancellationToken.None);

        Assert.Empty(logger.Messages);
    }

    [Fact]
    public async Task MissingSuccessfulRunRaisesAnAlert()
    {
        var now = new DateTimeOffset(2026, 7, 16, 12, 0, 0, TimeSpan.Zero);
        await using var dbContext = CreateContext();
        var timeProvider = new FixedTimeProvider(now);
        var runStore = new ImportRunStore(dbContext, timeProvider);
        var facade = CreateFacade(
            dbContext,
            timeProvider,
            runStore,
            new RecordingBackgroundJobClient());
        var logger = new RecordingLogger<SourcingSynchronizationJob>();
        var job = new SourcingSynchronizationJob(
            facade,
            runStore,
            timeProvider,
            Options.Create(new SourcingSynchronizationOptions()),
            logger);

        await job.ExecuteAsync(SourcingSource.Boamp, CancellationToken.None);

        Assert.Single(logger.Messages);
    }

    private static ImportRun CompletedRun(DateTimeOffset queuedAt, DateTimeOffset finishedAt)
    {
        var run = new ImportRun(SourcingSource.Boamp, queuedAt);
        run.TryStart(queuedAt);
        run.Complete(finishedAt);
        return run;
    }

    private static SourcingFacade CreateFacade(
        RiposteDbContext dbContext,
        TimeProvider timeProvider,
        ImportRunStore runStore,
        RecordingBackgroundJobClient backgroundJobs)
    {
        var source = new EmptySource();
        var settingsStore = new SourcingSettingsStore(dbContext, timeProvider);
        var importer = new OpportunityImporter(
            [source],
            dbContext,
            timeProvider,
            settingsStore,
            runStore);
        return new SourcingFacade(
            dbContext,
            [source],
            settingsStore,
            runStore,
            importer,
            backgroundJobs,
            new RecordingRecurringJobManager());
    }

    private static RiposteDbContext CreateContext() => new(
        new DbContextOptionsBuilder<RiposteDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class EmptySource : IOpportunitySource
    {
        public string Key => SourcingSource.Boamp;

        public DateOnly GetStartDate(DateOnly today, DateOnly? lastSuccessfulDate) => today;

        public async IAsyncEnumerable<SourcingPage> ReadPagesAsync(
            SourcingSettings settings,
            DateOnly startDate,
            DateOnly endDate,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            yield break;
        }
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) => Messages.Add(formatter(state, exception));
    }
}
