using Microsoft.EntityFrameworkCore;
using RiposteOS.Core.Sourcing;
using RiposteOS.Infrastructure.Persistence;
using RiposteOS.Infrastructure.Sourcing;

namespace RiposteOS.Tests.Sourcing;

public sealed class ImportRunStoreTests
{
    [Fact]
    public async Task ReconcileFailsOnlyStaleRunsForTheRequestedSource()
    {
        var now = new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);
        await using var dbContext = CreateContext();
        var staleRun = new ImportRun(SourcingSource.Boamp, now.AddHours(-3));
        var freshRun = new ImportRun(SourcingSource.Boamp, now.AddHours(-1));
        var otherSourceRun = new ImportRun(SourcingSource.Ted, now.AddHours(-3));
        dbContext.AddRange(staleRun, freshRun, otherSourceRun);
        await dbContext.SaveChangesAsync();
        var store = new ImportRunStore(dbContext, new FixedTimeProvider(now));

        var reconciled = await store.ReconcileAsync(SourcingSource.Boamp, CancellationToken.None);

        Assert.Equal(1, reconciled);
        Assert.Equal(ImportRunStatus.Failed, staleRun.Status);
        Assert.Equal(ImportRunStatus.Queued, freshRun.Status);
        Assert.Equal(ImportRunStatus.Queued, otherSourceRun.Status);
    }

    [Fact]
    public async Task StaleRunIsFailedBeforeANewRunIsQueued()
    {
        var now = new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);
        await using var dbContext = CreateContext();
        var staleRun = new ImportRun(SourcingSource.Boamp, now.AddHours(-3));
        dbContext.Add(staleRun);
        await dbContext.SaveChangesAsync();
        var store = new ImportRunStore(dbContext, new FixedTimeProvider(now));

        var result = await store.QueueAsync(SourcingSource.Boamp, CancellationToken.None);

        Assert.True(result.Created);
        Assert.NotEqual(staleRun.Id, result.Run.Id);
        Assert.Equal(ImportRunStatus.Failed, staleRun.Status);
    }

    [Fact]
    public async Task LifecycleMethodsIgnoreTerminalAndMissingRuns()
    {
        var now = new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);
        await using var dbContext = CreateContext();
        var store = new ImportRunStore(dbContext, new FixedTimeProvider(now));
        var queued = (await store.QueueAsync(SourcingSource.Boamp, CancellationToken.None)).Run;

        Assert.True(await store.StartAsync(queued.Id, CancellationToken.None));
        await store.CompleteAsync(queued.Id, CancellationToken.None);
        Assert.False(await store.StartAsync(queued.Id, CancellationToken.None));
        await store.FailAsync(queued.Id, "ignored", CancellationToken.None);
        await store.FailAsync(Guid.NewGuid(), "ignored", CancellationToken.None);

        Assert.Equal(ImportRunStatus.Succeeded, queued.Status);
        Assert.Null(queued.ErrorMessage);
    }

    [Fact]
    public async Task RunningRunCannotBeClaimedByASecondJob()
    {
        var now = new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);
        await using var dbContext = CreateContext();
        var store = new ImportRunStore(dbContext, new FixedTimeProvider(now));
        var run = (await store.QueueAsync(SourcingSource.Boamp, CancellationToken.None)).Run;

        Assert.True(await store.StartAsync(run.Id, CancellationToken.None));
        Assert.False(await store.StartAsync(run.Id, CancellationToken.None));
        Assert.Equal(now, run.StartedAt);
    }

    [Fact]
    public async Task ActiveRunCanBeFailed()
    {
        var now = new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);
        await using var dbContext = CreateContext();
        var store = new ImportRunStore(dbContext, new FixedTimeProvider(now));
        var queued = (await store.QueueAsync(SourcingSource.Boamp, CancellationToken.None)).Run;

        await store.FailAsync(queued.Id, "failed", CancellationToken.None);

        var persisted = await dbContext.Set<ImportRun>().SingleAsync();
        Assert.Equal(ImportRunStatus.Failed, persisted.Status);
        Assert.Equal("failed", persisted.ErrorMessage);
    }

    private static RiposteDbContext CreateContext() => new(
        new DbContextOptionsBuilder<RiposteDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
