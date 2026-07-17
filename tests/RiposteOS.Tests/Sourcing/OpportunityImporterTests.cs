using System.Runtime.CompilerServices;
using System.Text.Json;
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
    public async Task SharedEformsNoticeAttachesBoampAndTedToOneOpportunity()
    {
        await using var dbContext = new RiposteDbContext(
            new DbContextOptionsBuilder<RiposteDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
        var now = new DateTimeOffset(2026, 7, 17, 12, 0, 0, TimeSpan.Zero);
        var noticeId = Guid.Parse("3d11385e-9d2c-4649-afb2-a7ee15cf2cce");
        var boamp = new StubOpportunitySource(new SourceOpportunity(
            "26-68353",
            "Création d'un site internet",
            "OPCO 2i",
            new DateOnly(2026, 7, 17),
            now.AddDays(30),
            ["FRA"],
            ["75"],
            ["72413000"],
            [],
            [],
            "https://boamp.test/26-68353",
            "{\"source\":\"boamp\"}",
            EformsNoticeId: noticeId));
        var ted = new StubOpportunitySource(new SourceOpportunity(
            "478263-2026",
            "France – Services de développement de sites WWW – Création d'un site internet",
            "OPCO 2i",
            new DateOnly(2026, 7, 17),
            now.AddDays(30),
            ["FRA"],
            ["75"],
            ["72413000"],
            [],
            [],
            "https://ted.test/478263-2026",
            "{\"source\":\"ted\"}",
            EformsNoticeId: noticeId))
        {
            Key = SourcingSource.Ted,
        };
        var timeProvider = new FixedTimeProvider(now);
        var settingsStore = new SourcingSettingsStore(dbContext, timeProvider);
        await settingsStore.UpdateAsync(TestSourcingProfiles.Create(["site internet"]), CancellationToken.None);
        var runStore = new ImportRunStore(dbContext, timeProvider);
        var importer = new OpportunityImporter(
            [boamp, ted], dbContext, timeProvider, settingsStore, runStore);
        var job = new SourcingImportJob(importer, runStore, NullLogger<SourcingImportJob>.Instance);

        var boampRun = (await runStore.QueueAsync(SourcingSource.Boamp, CancellationToken.None)).Run;
        await job.ExecuteAsync(new ImportOpportunities(SourcingSource.Boamp, boampRun.Id), CancellationToken.None);
        var tedRun = (await runStore.QueueAsync(SourcingSource.Ted, CancellationToken.None)).Run;
        await job.ExecuteAsync(new ImportOpportunities(SourcingSource.Ted, tedRun.Id), CancellationToken.None);

        dbContext.ChangeTracker.Clear();
        var opportunity = await dbContext.Set<Opportunity>().SingleAsync();
        var publications = await dbContext.Set<OpportunityPublication>()
            .OrderBy(publication => publication.Source)
            .ToArrayAsync();
        Assert.Equal(noticeId, opportunity.EformsNoticeId);
        Assert.Equal("Création d'un site internet", opportunity.Title);
        Assert.Equal([SourcingSource.Boamp, SourcingSource.Ted], publications.Select(item => item.Source));
        Assert.Equal(0, (await dbContext.Set<ImportRun>().SingleAsync(run => run.Id == tedRun.Id)).Created);
    }

    [Fact]
    public async Task ExplicitReferenceAttachesASecondaryPublicationWithoutReplacingCanonicalData()
    {
        await using var dbContext = new RiposteDbContext(
            new DbContextOptionsBuilder<RiposteDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
        var now = new DateTimeOffset(2026, 7, 17, 12, 0, 0, TimeSpan.Zero);
        var timeProvider = new FixedTimeProvider(now);
        var boamp = new StubOpportunitySource(new SourceOpportunity(
            "26-1",
            "Titre BOAMP canonique",
            "Acheteur",
            new DateOnly(2026, 7, 17),
            null,
            ["FRA"],
            ["69"],
            ["72200000"],
            [],
            [],
            "https://boamp.test/26-1",
            "{\"source\":\"boamp\"}"));
        var placeOpportunity = new SourceOpportunity(
            "PLACE-1",
            "Titre PLACE différent",
            "Acheteur",
            new DateOnly(2026, 7, 17),
            null,
            ["FRA"],
            ["69"],
            ["72200000"],
            [],
            [],
            "https://place.test/PLACE-1",
            "{\"source\":\"place\",\"version\":1}",
            DocumentUrl: "https://place.test/dce")
        {
            References = [new SourceOpportunityReference(SourcingSource.Boamp, "26-1")],
        };
        var place = new StubOpportunitySource(placeOpportunity) { Key = "PLACE" };
        var settingsStore = new SourcingSettingsStore(dbContext, timeProvider);
        await settingsStore.UpdateAsync(TestSourcingProfiles.Create(["logiciel"]), CancellationToken.None);
        var runStore = new ImportRunStore(dbContext, timeProvider);
        var importer = new OpportunityImporter(
            [boamp, place], dbContext, timeProvider, settingsStore, runStore);
        var job = new SourcingImportJob(importer, runStore, NullLogger<SourcingImportJob>.Instance);

        var boampRun = (await runStore.QueueAsync(SourcingSource.Boamp, CancellationToken.None)).Run;
        await job.ExecuteAsync(new ImportOpportunities(SourcingSource.Boamp, boampRun.Id), CancellationToken.None);
        var placeRun = (await runStore.QueueAsync("PLACE", CancellationToken.None)).Run;
        await job.ExecuteAsync(new ImportOpportunities("PLACE", placeRun.Id), CancellationToken.None);

        dbContext.ChangeTracker.Clear();
        var opportunity = await dbContext.Set<Opportunity>().SingleAsync();
        var publications = await dbContext.Set<OpportunityPublication>()
            .OrderBy(publication => publication.Source)
            .ToArrayAsync();
        var completedPlaceRun = await dbContext.Set<ImportRun>().SingleAsync(run => run.Id == placeRun.Id);
        Assert.Equal("Titre BOAMP canonique", opportunity.Title);
        Assert.Equal(SourcingSource.Boamp, opportunity.Source);
        Assert.Equal(2, publications.Length);
        Assert.Equal([SourcingSource.Boamp, "PLACE"], publications.Select(item => item.Source));
        Assert.Equal(0, completedPlaceRun.Created);
        Assert.Equal(1, completedPlaceRun.Updated);
        Assert.Empty(await dbContext.Set<OpportunityRevision>().ToArrayAsync());

        place.Replace(new SourceOpportunity(
            "PLACE-2",
            "Titre BOAMP canonique",
            "Acheteur",
            new DateOnly(2026, 7, 17),
            null,
            ["FRA"],
            ["69"],
            ["72200000"],
            [],
            [],
            "https://place.test/PLACE-2",
            "{\"source\":\"place\",\"id\":2}"));
        var unreferencedRun = (await runStore.QueueAsync("PLACE", CancellationToken.None)).Run;
        await job.ExecuteAsync(
            new ImportOpportunities("PLACE", unreferencedRun.Id), CancellationToken.None);
        dbContext.ChangeTracker.Clear();

        Assert.Equal(2, await dbContext.Set<Opportunity>().CountAsync());
        Assert.Equal(3, await dbContext.Set<OpportunityPublication>().CountAsync());
        Assert.Equal(
            1,
            (await dbContext.Set<ImportRun>().SingleAsync(run => run.Id == unreferencedRun.Id)).Created);
    }

    [Fact]
    public async Task SkippedNoticeIsPersistedAndCanBeRetriedIdempotently()
    {
        await using var dbContext = new RiposteDbContext(
            new DbContextOptionsBuilder<RiposteDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
        var now = new DateTimeOffset(2026, 7, 16, 12, 0, 0, TimeSpan.Zero);
        var timeProvider = new FixedTimeProvider(now);
        var source = new RecoverableIssueSource();
        var settingsStore = new SourcingSettingsStore(dbContext, timeProvider);
        await settingsStore.UpdateAsync(
            TestSourcingProfiles.Create(["logiciel"]),
            CancellationToken.None);
        var runStore = new ImportRunStore(dbContext, timeProvider);
        var importer = new OpportunityImporter(
            [source],
            dbContext,
            timeProvider,
            settingsStore,
            runStore);
        var run = (await runStore.QueueAsync(SourcingSource.Boamp, CancellationToken.None)).Run;
        var job = new SourcingImportJob(importer, runStore, NullLogger<SourcingImportJob>.Instance);

        await job.ExecuteAsync(
            new ImportOpportunities(SourcingSource.Boamp, run.Id),
            CancellationToken.None);

        var issue = await dbContext.Set<ImportIssue>().SingleAsync();
        var completedRun = await dbContext.Set<ImportRun>().SingleAsync(item => item.Id == run.Id);
        Assert.Equal(ImportRunStatus.PartiallyFailed, completedRun.Status);
        Assert.Equal("recoverable", issue.SourceId);
        Assert.Null(issue.ResolvedAt);

        Assert.Null(await importer.RetryIssueAsync(Guid.NewGuid(), CancellationToken.None));
        var stillInvalid = await importer.RetryIssueAsync(issue.Id, CancellationToken.None);
        Assert.False(stillInvalid!.Resolved);
        Assert.Null(issue.ResolvedAt);

        source.CanParse = true;
        var retried = await importer.RetryIssueAsync(issue.Id, CancellationToken.None);
        var repeated = await importer.RetryIssueAsync(issue.Id, CancellationToken.None);

        Assert.True(retried!.Resolved);
        Assert.True(repeated!.Resolved);
        Assert.NotNull(issue.ResolvedAt);
        Assert.Single(await dbContext.Set<Opportunity>().ToArrayAsync());
    }

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
    public async Task ProfileChangeReassessesAnUnchangedNoticeWithoutCreatingARevision()
    {
        await using var dbContext = new RiposteDbContext(
            new DbContextOptionsBuilder<RiposteDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
        var now = new DateTimeOffset(2026, 7, 16, 12, 0, 0, TimeSpan.Zero);
        var timeProvider = new FixedTimeProvider(now);
        var source = new StubOpportunitySource(new SourceOpportunity(
            "profile-change",
            "Logiciel métier",
            "Acheteur",
            new DateOnly(2026, 7, 16),
            null,
            ["FRA"],
            ["69"],
            ["72200000"],
            [],
            [],
            string.Empty,
            "{\"idweb\":\"profile-change\"}"));
        var settingsStore = new SourcingSettingsStore(dbContext, timeProvider);
        var initialProfile = TestSourcingProfiles.Create(["logiciel"]);
        await settingsStore.UpdateAsync(initialProfile, CancellationToken.None);
        var runStore = new ImportRunStore(dbContext, timeProvider);
        var importer = new OpportunityImporter(
            [source],
            dbContext,
            timeProvider,
            settingsStore,
            runStore);
        var job = new SourcingImportJob(importer, runStore, NullLogger<SourcingImportJob>.Instance);
        var firstRun = (await runStore.QueueAsync(SourcingSource.Boamp, CancellationToken.None)).Run;
        await job.ExecuteAsync(
            new ImportOpportunities(SourcingSource.Boamp, firstRun.Id),
            CancellationToken.None);
        var initialOpportunity = await dbContext.Set<Opportunity>().SingleAsync();
        var initialScore = initialOpportunity.MatchScore;
        var initialReasons = initialOpportunity.MatchReasons.ToArray();
        Assert.True(initialScore > 0);

        await settingsStore.UpdateAsync(
            initialProfile with { PositiveSignals = ["logiciel"] },
            CancellationToken.None);
        var secondRun = (await runStore.QueueAsync(SourcingSource.Boamp, CancellationToken.None)).Run;
        await job.ExecuteAsync(
            new ImportOpportunities(SourcingSource.Boamp, secondRun.Id),
            CancellationToken.None);
        dbContext.ChangeTracker.Clear();
        var rescored = await dbContext.Set<Opportunity>().SingleAsync();
        Assert.Equal(initialScore, rescored.MatchScore);
        Assert.NotEqual(initialReasons, rescored.MatchReasons);
        Assert.Contains("+15 Signal positif : logiciel", rescored.MatchReasons);
        Assert.Equal(
            1,
            (await dbContext.Set<ImportRun>().SingleAsync(run => run.Id == secondRun.Id)).Unchanged);

        await settingsStore.UpdateAsync(
            initialProfile with
            {
                PositiveSignals = [],
                PreferredDepartmentCodes = [],
                CpvWhitelistPrefixes = [],
                CpvWatchPrefixes = [],
            },
            CancellationToken.None);
        var thirdRun = (await runStore.QueueAsync(SourcingSource.Boamp, CancellationToken.None)).Run;
        await job.ExecuteAsync(
            new ImportOpportunities(SourcingSource.Boamp, thirdRun.Id),
            CancellationToken.None);

        dbContext.ChangeTracker.Clear();
        Assert.Equal(0, (await dbContext.Set<Opportunity>().SingleAsync()).MatchScore);
        Assert.Equal(1, (await dbContext.Set<ImportRun>().SingleAsync(run => run.Id == thirdRun.Id)).Unchanged);
        Assert.Empty(await dbContext.Set<OpportunityRevision>().ToArrayAsync());
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
                ["FRA"],
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
                ["FRA"],
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
        foreach (var opportunity in await dbContext.Set<Opportunity>().ToArrayAsync())
        {
            dbContext.Entry(opportunity).Property(item => item.ContentHash).CurrentValue = "";
        }

        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        var secondRun = (await runStore.QueueAsync(SourcingSource.Boamp, CancellationToken.None)).Run;
        await job.ExecuteAsync(
            new ImportOpportunities(SourcingSource.Boamp, secondRun.Id),
            CancellationToken.None);
        source.Replace(
            new SourceOpportunity(
                "26-59690",
                "Développement d'un logiciel métier mis à jour",
                "Acheteur public",
                new DateOnly(2026, 6, 18),
                new DateTimeOffset(2026, 7, 17, 14, 0, 0, TimeSpan.Zero),
                ["FRA"],
                ["69"],
                ["72200000"],
                ["186"],
                ["Logiciel"],
                "https://www.boamp.fr/pages/avis/?q=idweb:26-59690",
                "{\"idweb\":\"26-59690\",\"version\":2}"),
            new SourceOpportunity(
                "26-59691",
                "Refonte d'un portail usager",
                "Autre acheteur public",
                new DateOnly(2026, 6, 18),
                null,
                ["FRA"],
                ["69"],
                ["72400000"],
                ["186"],
                ["Logiciel"],
                "https://www.boamp.fr/pages/avis/?q=idweb:26-59691",
                "{\"idweb\":\"26-59691\"}"));
        dbContext.ChangeTracker.Clear();
        var thirdRun = (await runStore.QueueAsync(SourcingSource.Boamp, CancellationToken.None)).Run;
        await job.ExecuteAsync(
            new ImportOpportunities(SourcingSource.Boamp, thirdRun.Id),
            CancellationToken.None);

        var runs = await dbContext.Set<ImportRun>().OrderBy(run => run.QueuedAt).ToArrayAsync();
        Assert.Equal(3, runs.Length);
        Assert.All(runs, run => Assert.Equal(ImportRunStatus.Succeeded, run.Status));
        Assert.Contains(runs, run => run.Created == 2);
        Assert.Contains(runs, run => run.Updated == 0 && run.Created == 0 && run.Unchanged == 2);
        Assert.Contains(runs, run => run.Updated == 1 && run.Unchanged == 1);
        var persistedOpportunities = await dbContext.Set<Opportunity>().ToArrayAsync();
        Assert.Equal(2, persistedOpportunities.Length);
        Assert.All(persistedOpportunities, opportunity => Assert.Equal(64, opportunity.ContentHash.Length));
        var revisions = await dbContext.Set<OpportunityRevision>()
            .OrderBy(revision => revision.RawPayload)
            .ToArrayAsync();
        var revision = Assert.Single(revisions);
        Assert.Equal("{\"idweb\":\"26-59690\"}", revision.RawPayload);
        Assert.DoesNotContain("\"version\":2", revision.RawPayload, StringComparison.Ordinal);
        Assert.Equal(
            new DateOnly(2026, 6, 18),
            (await dbContext.Set<SourcingSyncState>().SingleAsync()).LastSuccessfulPublicationDate);
        Assert.Equal(["logiciel métier"], source.Settings!.Keywords);
        Assert.Equal(["porte automatique"], source.Settings.ExcludedKeywords);
        Assert.Equal(1, source.Settings.PageSize);
        Assert.Equal([null, new DateOnly(2026, 6, 18), new DateOnly(2026, 6, 18)], source.Cursors);
    }

    private sealed class StubOpportunitySource(params SourceOpportunity[] opportunities)
        : IOpportunitySource
    {
        private SourceOpportunity[] _opportunities = opportunities;

        public string Key { get; init; } = SourcingSource.Boamp;

        public SourcingSettings? Settings { get; private set; }

        public List<DateOnly?> Cursors { get; } = [];

        public void Replace(params SourceOpportunity[] replacements) => _opportunities = replacements;

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

            foreach (var page in _opportunities.Chunk(settings.PageSize))
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return new SourcingPage(startDate, page.Length, page, 0);
                await Task.Yield();
            }
        }
    }

    private sealed class RecoverableIssueSource : IOpportunitySource
    {
        private const string RawPayload = "{\"idweb\":\"recoverable\"}";

        public string Key => SourcingSource.Boamp;

        public bool CanParse { get; set; }

        public DateOnly GetStartDate(DateOnly today, DateOnly? lastSuccessfulDate) => today;

        public async IAsyncEnumerable<SourcingPage> ReadPagesAsync(
            SourcingSettings settings,
            DateOnly startDate,
            DateOnly endDate,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            yield return new SourcingPage(
                startDate,
                1,
                [],
                1,
                [new SourceImportIssue("recoverable", "mapping_json", RawPayload)]);
        }

        public SourceOpportunity ParseRawOpportunity(string rawPayload)
        {
            if (!CanParse)
            {
                throw new JsonException("The source mapping has not been fixed yet.");
            }

            Assert.Equal(RawPayload, rawPayload);
            return new SourceOpportunity(
                "recoverable",
                "Développement d'un logiciel",
                "Acheteur",
                new DateOnly(2026, 7, 16),
                null,
                ["FRA"],
                ["69"],
                ["72200000"],
                [],
                [],
                string.Empty,
                RawPayload);
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
