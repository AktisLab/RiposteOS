using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RiposteOS.Core.Sourcing;
using RiposteOS.Infrastructure.Persistence;
using RiposteOS.Infrastructure.Sourcing;
using RiposteOS.Tests.TestSupport;

namespace RiposteOS.Tests.Sourcing;

public sealed class OpportunityImporterTests
{
    [Fact]
    public async Task UnregisteredSourceIsRejected()
    {
        await using var dbContext = new RiposteDbContext(
            new DbContextOptionsBuilder<RiposteDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
        var settingsStore = new SourcingSettingsStore(dbContext, TimeProvider.System);
        var importer = new OpportunityImporter(
            [],
            dbContext,
            TimeProvider.System,
            settingsStore,
            new ImportRunStore(dbContext, TimeProvider.System));

        var exception = await Assert.ThrowsAsync<NotSupportedException>(() => importer.ImportAsync(
            new ImportOpportunities("TED", Guid.NewGuid()),
            CancellationToken.None));

        Assert.Contains("TED", exception.Message);
    }

    [Fact]
    public async Task CompletedImportsAdvanceTheCursorWithoutCreatingDuplicates()
    {
        await using var dbContext = new RiposteDbContext(
            new DbContextOptionsBuilder<RiposteDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
        var now = new DateTimeOffset(2026, 6, 18, 12, 0, 0, TimeSpan.Zero);
        var timeProvider = new FixedTimeProvider(now);
        var source = new StubOpportunitySource(
            new SourceOpportunity(
                "26-59690",
                "Développement d'un logiciel métier",
                "Acheteur public",
                new DateOnly(2026, 6, 18),
                new DateTimeOffset(2026, 7, 17, 14, 0, 0, TimeSpan.Zero),
                ["69"],
                ["72200000"],
                ["186"],
                ["Logiciel"],
                "https://www.boamp.fr/pages/avis/?q=idweb:26-59690",
                "{\"idweb\":\"26-59690\"}"),
            new SourceOpportunity(
                "26-59691",
                "Refonte d'un portail usager",
                "Autre acheteur public",
                new DateOnly(2026, 6, 18),
                null,
                ["69"],
                ["72400000"],
                ["186"],
                ["Logiciel"],
                "https://www.boamp.fr/pages/avis/?q=idweb:26-59691",
                "{\"idweb\":\"26-59691\"}"));
        var settingsStore = new SourcingSettingsStore(dbContext, timeProvider);
        await settingsStore.UpdateAsync(
            TestSourcingProfiles.Create(
                ["logiciel métier"],
                ["porte automatique"],
                pageSize: 1),
            CancellationToken.None);
        var runStore = new ImportRunStore(dbContext, timeProvider);
        var importer = new OpportunityImporter(
            [source],
            dbContext,
            timeProvider,
            settingsStore,
            runStore);
        var job = new SourcingImportJob(
            importer,
            runStore,
            NullLogger<SourcingImportJob>.Instance);

        var firstRun = (await runStore.QueueAsync(SourcingSource.Boamp, CancellationToken.None)).Run;
        await job.ExecuteAsync(
            new ImportOpportunities(SourcingSource.Boamp, firstRun.Id),
            CancellationToken.None);
        dbContext.ChangeTracker.Clear();
        var secondRun = (await runStore.QueueAsync(SourcingSource.Boamp, CancellationToken.None)).Run;
        await job.ExecuteAsync(
            new ImportOpportunities(SourcingSource.Boamp, secondRun.Id),
            CancellationToken.None);

        var runs = await dbContext.Set<ImportRun>().OrderBy(run => run.QueuedAt).ToArrayAsync();
        Assert.Equal(2, runs.Length);
        Assert.All(runs, run => Assert.Equal(ImportRunStatus.Succeeded, run.Status));
        Assert.Contains(runs, run => run.Created == 2);
        Assert.Contains(runs, run => run.Updated == 2);
        Assert.Equal(2, await dbContext.Set<Opportunity>().CountAsync());
        Assert.Equal(
            new DateOnly(2026, 6, 18),
            (await dbContext.Set<SourcingSyncState>().SingleAsync()).LastSuccessfulPublicationDate);
        Assert.Equal(["logiciel métier"], source.Settings!.Keywords);
        Assert.Equal(["porte automatique"], source.Settings.ExcludedKeywords);
        Assert.Equal(1, source.Settings.PageSize);
        Assert.Equal([null, new DateOnly(2026, 6, 18)], source.Cursors);
    }

    private sealed class StubOpportunitySource(params SourceOpportunity[] opportunities)
        : IOpportunitySource
    {
        public string Key => SourcingSource.Boamp;

        public SourcingSettings? Settings { get; private set; }

        public List<DateOnly?> Cursors { get; } = [];

        public DateOnly GetStartDate(DateOnly today, DateOnly? lastSuccessfulDate)
        {
            Cursors.Add(lastSuccessfulDate);
            return lastSuccessfulDate ?? today;
        }

        public async IAsyncEnumerable<SourcingPage> ReadPagesAsync(
            SourcingSettings settings,
            DateOnly startDate,
            DateOnly endDate,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            Settings = settings;

            foreach (var page in opportunities.Chunk(settings.PageSize))
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return new SourcingPage(startDate, page.Length, page, 0);
                await Task.Yield();
            }
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
