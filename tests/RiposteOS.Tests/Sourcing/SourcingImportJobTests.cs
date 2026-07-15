using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RiposteOS.Core.Sourcing;
using RiposteOS.Infrastructure.Persistence;
using RiposteOS.Infrastructure.Sourcing;
using RiposteOS.Tests.TestSupport;

namespace RiposteOS.Tests.Sourcing;

public sealed class SourcingImportJobTests
{
    [Fact]
    public async Task TerminalRunIsNotExecutedAgain()
    {
        await using var dbContext = CreateContext();
        var runStore = new ImportRunStore(dbContext, TimeProvider.System);
        var run = (await runStore.QueueAsync(SourcingSource.Boamp, CancellationToken.None)).Run;
        await runStore.StartAsync(run.Id, CancellationToken.None);
        await runStore.CompleteAsync(run.Id, CancellationToken.None);
        var source = new ControlledSource(new InvalidOperationException("must not run"));
        var job = CreateJob(dbContext, runStore, source);

        await job.ExecuteAsync(
            new ImportOpportunities(SourcingSource.Boamp, run.Id),
            CancellationToken.None);

        Assert.False(source.WasRead);
        Assert.Equal(ImportRunStatus.Succeeded, run.Status);
    }

    [Fact]
    public async Task ImportFailureIsPersistedAndRethrown()
    {
        await using var dbContext = CreateContext();
        var runStore = new ImportRunStore(dbContext, TimeProvider.System);
        var run = (await runStore.QueueAsync(SourcingSource.Boamp, CancellationToken.None)).Run;
        var job = CreateJob(
            dbContext,
            runStore,
            new ControlledSource(new InvalidOperationException("provider failed")));

        await Assert.ThrowsAsync<InvalidOperationException>(() => job.ExecuteAsync(
            new ImportOpportunities(SourcingSource.Boamp, run.Id),
            CancellationToken.None));

        var persisted = await dbContext.Set<ImportRun>().SingleAsync();
        Assert.Equal(ImportRunStatus.Failed, persisted.Status);
        Assert.Contains(SourcingSource.Boamp, persisted.ErrorMessage);
    }

    [Fact]
    public async Task CancellationIsRethrownWithoutConvertingItToAProviderFailure()
    {
        await using var dbContext = CreateContext();
        var runStore = new ImportRunStore(dbContext, TimeProvider.System);
        var run = (await runStore.QueueAsync(SourcingSource.Boamp, CancellationToken.None)).Run;
        using var cancellation = new CancellationTokenSource();
        var job = CreateJob(
            dbContext,
            runStore,
            new ControlledSource(cancellation));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => job.ExecuteAsync(
            new ImportOpportunities(SourcingSource.Boamp, run.Id),
            cancellation.Token));

        Assert.Equal(ImportRunStatus.Running, run.Status);
        Assert.Null(run.ErrorMessage);
    }

    private static SourcingImportJob CreateJob(
        RiposteDbContext dbContext,
        ImportRunStore runStore,
        IOpportunitySource source)
    {
        dbContext.Add(new SourcingSettings(
            TestSourcingProfiles.Create(["logiciel"]),
            TimeProvider.System.GetUtcNow()));
        dbContext.SaveChanges();
        var settingsStore = new SourcingSettingsStore(dbContext, TimeProvider.System);
        var importer = new OpportunityImporter(
            [source],
            dbContext,
            TimeProvider.System,
            settingsStore,
            runStore);
        return new SourcingImportJob(
            importer,
            runStore,
            NullLogger<SourcingImportJob>.Instance);
    }

    private static RiposteDbContext CreateContext() => new(
        new DbContextOptionsBuilder<RiposteDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private sealed class ControlledSource : IOpportunitySource
    {
        private readonly Exception? _exception;
        private readonly CancellationTokenSource? _cancellation;

        public ControlledSource(Exception exception) => _exception = exception;

        public ControlledSource(CancellationTokenSource cancellation) => _cancellation = cancellation;

        public string Key => SourcingSource.Boamp;

        public bool WasRead { get; private set; }

        public DateOnly GetStartDate(DateOnly today, DateOnly? lastSuccessfulDate) => today;

        public async IAsyncEnumerable<SourcingPage> ReadPagesAsync(
            SourcingSettings settings,
            DateOnly startDate,
            DateOnly endDate,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            WasRead = true;
            await Task.Yield();
            if (_cancellation is not null)
            {
                _cancellation.Cancel();
                cancellationToken.ThrowIfCancellationRequested();
            }

            if (_exception is not null)
            {
                throw _exception;
            }

            yield break;
        }
    }
}
