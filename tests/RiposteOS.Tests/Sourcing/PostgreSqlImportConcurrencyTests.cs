using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using RiposteOS.Core.Sourcing;
using RiposteOS.Infrastructure.Persistence;
using RiposteOS.Infrastructure.Sourcing;
using RiposteOS.Tests.TestSupport;

namespace RiposteOS.Tests.Sourcing;

[Collection(PostgreSqlTestGroup.Name)]
public sealed class PostgreSqlImportConcurrencyTests(PostgreSqlFixture fixture)
{
    [Fact]
    public async Task MigrationBackfillsExistingBoampAndTedOpportunities()
    {
        const string PreviousMigration = "20260716174501_AddOpportunityEnrichment";
        var connectionString = await fixture.CreateDatabaseAsync(PreviousMigration);
        var importedAt = new DateTimeOffset(2026, 7, 17, 12, 0, 0, TimeSpan.Zero);
        await using (var seedContext = PostgreSqlFixture.CreateContext(connectionString))
        {
            seedContext.AddRange(
                CreateOpportunity("boamp-existing", importedAt),
                new Opportunity(
                    SourcingSource.Ted,
                    "ted-existing",
                    "Développement d'un logiciel métier",
                    "Acheteur public",
                    new DateOnly(2026, 7, 17),
                    null,
                    ["FRA"],
                    ["69"],
                    ["72200000"],
                    [],
                    [],
                    40,
                    [],
                    "https://ted.test/notice",
                    "{\"source\":\"ted\"}",
                    importedAt,
                    documentUrl: "https://ted.test/dce"));
            await seedContext.SaveChangesAsync();
            await seedContext.Database.MigrateAsync();
        }

        await using var verificationContext = PostgreSqlFixture.CreateContext(connectionString);
        var publications = await verificationContext.Set<OpportunityPublication>()
            .OrderBy(publication => publication.Source)
            .ToArrayAsync();
        Assert.Equal(2, publications.Length);
        Assert.Equal([SourcingSource.Boamp, SourcingSource.Ted], publications.Select(item => item.Source));
        Assert.Equal("https://ted.test/dce", publications[1].DocumentUrl);
        Assert.All(publications, publication => Assert.False(string.IsNullOrWhiteSpace(publication.ContentHash)));
    }

    [Fact]
    public async Task PublicationIdentityIsUniqueAcrossOpportunities()
    {
        var connectionString = await fixture.CreateDatabaseAsync();
        var now = new DateTimeOffset(2026, 7, 17, 12, 0, 0, TimeSpan.Zero);
        await using (var firstContext = PostgreSqlFixture.CreateContext(connectionString))
        {
            var opportunity = CreateOpportunity("first", now);
            opportunity.AddPublication(SourcingSource.Boamp, "26-shared", "", null, "{}", now);
            firstContext.Add(opportunity);
            await firstContext.SaveChangesAsync();
        }

        await using var secondContext = PostgreSqlFixture.CreateContext(connectionString);
        var duplicate = CreateOpportunity("second", now);
        duplicate.AddPublication(SourcingSource.Boamp, "26-shared", "", null, "{}", now);
        secondContext.Add(duplicate);

        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => secondContext.SaveChangesAsync());
        Assert.Equal(
            PostgresErrorCodes.UniqueViolation,
            Assert.IsType<PostgresException>(exception.InnerException).SqlState);
    }

    [Fact]
    public async Task ConcurrentQueuesKeepOneActiveRunPerSource()
    {
        var connectionString = await fixture.CreateDatabaseAsync();
        await using var firstContext = PostgreSqlFixture.CreateContext(connectionString);
        await using var secondContext = PostgreSqlFixture.CreateContext(connectionString);
        var now = new DateTimeOffset(2026, 7, 16, 12, 0, 0, TimeSpan.Zero);
        var firstStore = new ImportRunStore(firstContext, new FixedTimeProvider(now));
        var secondStore = new ImportRunStore(secondContext, new FixedTimeProvider(now));

        var results = await Task.WhenAll(
            firstStore.QueueAsync(SourcingSource.Boamp, CancellationToken.None),
            secondStore.QueueAsync(SourcingSource.Boamp, CancellationToken.None));

        Assert.Single(results, result => result.Created);
        Assert.Single(results, result => !result.Created);
        Assert.Equal(results[0].Run.Id, results[1].Run.Id);
    }

    [Fact]
    public async Task DuplicateJobDeliveryIsClaimedOnlyOnce()
    {
        var connectionString = await fixture.CreateDatabaseAsync();
        var now = new DateTimeOffset(2026, 7, 16, 12, 0, 0, TimeSpan.Zero);
        var timeProvider = new FixedTimeProvider(now);
        Guid runId;
        await using (var seedContext = PostgreSqlFixture.CreateContext(connectionString))
        {
            seedContext.Add(new SourcingSettings(TestSourcingProfiles.Create(["logiciel"]), now));
            var run = new ImportRun(SourcingSource.Boamp, now);
            seedContext.Add(run);
            await seedContext.SaveChangesAsync();
            runId = run.Id;
        }

        var source = new BlockingSource();
        await using var firstContext = PostgreSqlFixture.CreateContext(connectionString);
        await using var secondContext = PostgreSqlFixture.CreateContext(connectionString);
        var firstJob = CreateJob(firstContext, source, timeProvider);
        var secondJob = CreateJob(secondContext, source, timeProvider);
        var command = new ImportOpportunities(SourcingSource.Boamp, runId);

        var firstDelivery = firstJob.ExecuteAsync(command, CancellationToken.None);
        await source.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await secondJob.ExecuteAsync(command, CancellationToken.None);
        source.Release.TrySetResult();
        await firstDelivery;

        Assert.Equal(1, source.ReadCount);
        await using var verificationContext = PostgreSqlFixture.CreateContext(connectionString);
        Assert.Single(await verificationContext.Set<Opportunity>().ToArrayAsync());
        Assert.Equal(
            ImportRunStatus.Succeeded,
            (await verificationContext.Set<ImportRun>().SingleAsync()).Status);
    }

    [Fact]
    public async Task ConcurrentIssueRetriesDoNotDuplicateAnOpportunity()
    {
        var connectionString = await fixture.CreateDatabaseAsync();
        var now = new DateTimeOffset(2026, 7, 16, 12, 0, 0, TimeSpan.Zero);
        var timeProvider = new FixedTimeProvider(now);
        Guid firstIssueId;
        Guid secondIssueId;
        await using (var seedContext = PostgreSqlFixture.CreateContext(connectionString))
        {
            seedContext.Add(new SourcingSettings(TestSourcingProfiles.Create(["logiciel"]), now));
            var firstRun = FailedRun(now, SourcingSource.Boamp);
            var secondRun = FailedRun(now, SourcingSource.Boamp);
            seedContext.AddRange(firstRun, secondRun);
            await seedContext.SaveChangesAsync();
            var firstIssue = RetryIssue(firstRun.Id, now);
            var secondIssue = RetryIssue(secondRun.Id, now);
            seedContext.AddRange(firstIssue, secondIssue);
            await seedContext.SaveChangesAsync();
            firstIssueId = firstIssue.Id;
            secondIssueId = secondIssue.Id;
        }

        var barrier = new ParseBarrier(2);
        await using var firstContext = PostgreSqlFixture.CreateContext(connectionString);
        await using var secondContext = PostgreSqlFixture.CreateContext(connectionString);
        var firstImporter = CreateImporter(firstContext, new RetrySource(barrier), timeProvider);
        var secondImporter = CreateImporter(secondContext, new RetrySource(barrier), timeProvider);

        var results = await Task.WhenAll(
            firstImporter.RetryIssueAsync(firstIssueId, CancellationToken.None),
            secondImporter.RetryIssueAsync(secondIssueId, CancellationToken.None));

        Assert.All(results, result => Assert.True(result!.Resolved));
        await using var verificationContext = PostgreSqlFixture.CreateContext(connectionString);
        Assert.Single(await verificationContext.Set<Opportunity>().ToArrayAsync());
        Assert.All(
            await verificationContext.Set<ImportIssue>().ToArrayAsync(),
            issue => Assert.NotNull(issue.ResolvedAt));
    }

    [Fact]
    public async Task CrashAfterOnePageResumesWithoutDuplicatingCommittedData()
    {
        var connectionString = await fixture.CreateDatabaseAsync();
        var now = new DateTimeOffset(2026, 7, 16, 12, 0, 0, TimeSpan.Zero);
        var timeProvider = new FixedTimeProvider(now);
        var source = new CrashOnceSource();
        await using var dbContext = PostgreSqlFixture.CreateContext(connectionString);
        dbContext.Add(new SourcingSettings(TestSourcingProfiles.Create(["logiciel"]), now));
        await dbContext.SaveChangesAsync();
        var runStore = new ImportRunStore(dbContext, timeProvider);
        var importer = new OpportunityImporter(
            [source],
            dbContext,
            timeProvider,
            new SourcingSettingsStore(dbContext, timeProvider),
            runStore);
        var job = new SourcingImportJob(importer, runStore, NullLogger<SourcingImportJob>.Instance);
        var firstRun = (await runStore.QueueAsync(SourcingSource.Boamp, CancellationToken.None)).Run;

        await Assert.ThrowsAsync<InvalidOperationException>(() => job.ExecuteAsync(
            new ImportOpportunities(SourcingSource.Boamp, firstRun.Id),
            CancellationToken.None));
        var secondRun = (await runStore.QueueAsync(SourcingSource.Boamp, CancellationToken.None)).Run;
        await job.ExecuteAsync(
            new ImportOpportunities(SourcingSource.Boamp, secondRun.Id),
            CancellationToken.None);

        dbContext.ChangeTracker.Clear();
        var failedRun = await dbContext.Set<ImportRun>().SingleAsync(run => run.Id == firstRun.Id);
        var succeededRun = await dbContext.Set<ImportRun>().SingleAsync(run => run.Id == secondRun.Id);
        Assert.Equal(ImportRunStatus.Failed, failedRun.Status);
        Assert.Equal(1, failedRun.Created);
        Assert.Equal(ImportRunStatus.Succeeded, succeededRun.Status);
        Assert.Equal(1, succeededRun.Created);
        Assert.Equal(1, succeededRun.Unchanged);
        Assert.Equal(2, await dbContext.Set<Opportunity>().CountAsync());
        Assert.Equal(
            new DateOnly(2026, 7, 16),
            (await dbContext.Set<SourcingSyncState>().SingleAsync()).LastSuccessfulPublicationDate);
    }

    private static SourcingImportJob CreateJob(
        RiposteDbContext dbContext,
        IOpportunitySource source,
        TimeProvider timeProvider)
    {
        var runStore = new ImportRunStore(dbContext, timeProvider);
        return new SourcingImportJob(
            CreateImporter(dbContext, source, timeProvider, runStore),
            runStore,
            NullLogger<SourcingImportJob>.Instance);
    }

    private static OpportunityImporter CreateImporter(
        RiposteDbContext dbContext,
        IOpportunitySource source,
        TimeProvider timeProvider,
        ImportRunStore? runStore = null) =>
        new(
            [source],
            dbContext,
            timeProvider,
            new SourcingSettingsStore(dbContext, timeProvider),
            runStore ?? new ImportRunStore(dbContext, timeProvider));

    private static ImportRun FailedRun(DateTimeOffset queuedAt, string source)
    {
        var run = new ImportRun(source, queuedAt);
        run.Fail("mapping failure", queuedAt);
        return run;
    }

    private static ImportIssue RetryIssue(Guid runId, DateTimeOffset createdAt) =>
        new(
            runId,
            SourcingSource.Boamp,
            "same-notice",
            "mapping_json",
            "{\"idweb\":\"same-notice\"}",
            createdAt);

    private static SourceOpportunity Opportunity(string sourceId) => new(
        sourceId,
        "Développement d'un logiciel métier",
        "Acheteur public",
        new DateOnly(2026, 7, 16),
        null,
        ["FRA"],
        ["69"],
        ["72200000"],
        [],
        [],
        string.Empty,
        $"{{\"idweb\":\"{sourceId}\"}}");

    private static Opportunity CreateOpportunity(string sourceId, DateTimeOffset importedAt) => new(
        SourcingSource.Boamp,
        sourceId,
        "Développement d'un logiciel métier",
        "Acheteur public",
        new DateOnly(2026, 7, 17),
        null,
        ["FRA"],
        ["69"],
        ["72200000"],
        [],
        [],
        40,
        [],
        string.Empty,
        "{}",
        importedAt);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class BlockingSource : IOpportunitySource
    {
        private int _readCount;

        public string Key => SourcingSource.Boamp;

        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int ReadCount => _readCount;

        public DateOnly GetStartDate(DateOnly today, DateOnly? lastSuccessfulDate) => today;

        public async IAsyncEnumerable<SourcingPage> ReadPagesAsync(
            SourcingSettings settings,
            DateOnly startDate,
            DateOnly endDate,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _readCount);
            Started.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
            yield return new SourcingPage(startDate, 1, [Opportunity("single")], 0);
        }
    }

    private sealed class ParseBarrier(int participantCount)
    {
        private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _arrived;

        public async Task WaitAsync()
        {
            if (Interlocked.Increment(ref _arrived) == participantCount)
            {
                _ready.TrySetResult();
            }

            await _ready.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
    }

    private sealed class RetrySource(ParseBarrier barrier) : IOpportunitySource
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

        public SourceOpportunity ParseRawOpportunity(string rawPayload)
        {
            barrier.WaitAsync().GetAwaiter().GetResult();
            return Opportunity("same-notice");
        }
    }

    private sealed class CrashOnceSource : IOpportunitySource
    {
        private int _attempt;

        public string Key => SourcingSource.Boamp;

        public DateOnly GetStartDate(DateOnly today, DateOnly? lastSuccessfulDate) =>
            lastSuccessfulDate ?? today;

        public async IAsyncEnumerable<SourcingPage> ReadPagesAsync(
            SourcingSettings settings,
            DateOnly startDate,
            DateOnly endDate,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var attempt = Interlocked.Increment(ref _attempt);
            yield return new SourcingPage(startDate, 1, [Opportunity("page-one")], 0);
            await Task.Yield();
            if (attempt == 1)
            {
                throw new InvalidOperationException("Simulated provider crash after page one.");
            }

            yield return new SourcingPage(startDate, 1, [Opportunity("page-two")], 0);
        }
    }
}
