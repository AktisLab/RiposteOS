using Microsoft.EntityFrameworkCore;
using RiposteOS.Core.Sourcing;
using RiposteOS.Infrastructure.Persistence;
using RiposteOS.Infrastructure.Sourcing;

namespace RiposteOS.Tests.Sourcing;

public sealed class SourcingSettingsStoreTests
{
    [Fact]
    public async Task MissingProfileReturnsNull()
    {
        await using var dbContext = CreateContext();
        var store = new SourcingSettingsStore(dbContext, TimeProvider.System);

        Assert.Null(await store.GetAsync(CancellationToken.None));
    }

    [Fact]
    public async Task CreatingAndUpdatingSettingsClearsEverySourceCursor()
    {
        var now = new DateTimeOffset(2026, 7, 15, 10, 0, 0, TimeSpan.Zero);
        await using var dbContext = CreateContext();
        dbContext.Add(new SourcingSyncState(SourcingSource.Boamp));
        dbContext.Add(new SourcingSyncState("TED"));
        dbContext.Add(new Opportunity(
            SourcingSource.Boamp,
            "26-000001",
            "Tierce maintenance applicative",
            "Acheteur",
            new DateOnly(2026, 7, 15),
            now.AddDays(30),
            ["FRA"],
            [],
            ["72267100"],
            [],
            [],
            40,
            ["+25 CPV ciblé : 72267100"],
            "",
            "{}",
            now));
        await dbContext.SaveChangesAsync();
        var store = new SourcingSettingsStore(
            dbContext,
            new FixedTimeProvider(now.AddHours(1)));

        var settings = await store.UpdateAsync(
            TestSupport.TestSourcingProfiles.Create(["new"]) with
            {
                NegativeSignals = ["maintenance applicative"],
                CpvWhitelistPrefixes = [],
                CpvExcludedPrefixes = ["72267"],
            },
            CancellationToken.None);

        Assert.Equal(["new"], settings.Keywords);
        Assert.Empty(await dbContext.Set<SourcingSyncState>().ToArrayAsync());
        Assert.Same(settings, await store.GetAsync(CancellationToken.None));
        var opportunity = await dbContext.Set<Opportunity>().SingleAsync();
        Assert.Equal(0, opportunity.MatchScore);
        Assert.Contains("-30 Signal négatif : maintenance applicative", opportunity.MatchReasons);
        Assert.Contains("-50 CPV exclu : 72267100", opportunity.MatchReasons);

        var updated = await store.UpdateAsync(
            TestSupport.TestSourcingProfiles.Create(["updated"]),
            CancellationToken.None);

        Assert.Same(settings, updated);
        Assert.Equal(["updated"], updated.Keywords);
    }

    [Fact]
    public async Task UpdatingCountryScopeDeletesOutOfScopeOpportunitiesAndTheirRevisions()
    {
        var now = new DateTimeOffset(2026, 7, 16, 10, 0, 0, TimeSpan.Zero);
        await using var dbContext = CreateContext();
        var store = new SourcingSettingsStore(dbContext, new FixedTimeProvider(now));
        await store.UpdateAsync(
            TestSupport.TestSourcingProfiles.Create() with
            {
                AllowedCountryCodes = ["FRA", "BEL"],
            },
            CancellationToken.None);
        var french = CreateOpportunity("france", "FRA", now);
        var belgian = CreateOpportunity("belgium", "BEL", now);
        dbContext.AddRange(french, belgian);
        await dbContext.SaveChangesAsync();
        dbContext.Add(new OpportunityRevision(belgian, now.AddMinutes(1)));
        await dbContext.SaveChangesAsync();

        await store.UpdateAsync(
            TestSupport.TestSourcingProfiles.Create() with
            {
                AllowedCountryCodes = ["FRA"],
            },
            CancellationToken.None);

        Assert.Equal("france", (await dbContext.Set<Opportunity>().SingleAsync()).SourceId);
        Assert.Empty(await dbContext.Set<OpportunityRevision>().ToArrayAsync());
    }

    private static Opportunity CreateOpportunity(
        string sourceId,
        string countryCode,
        DateTimeOffset importedAt) => new(
        SourcingSource.Ted,
        sourceId,
        "Logiciel métier",
        "Acheteur",
        new DateOnly(2026, 7, 16),
        importedAt.AddDays(30),
        [countryCode],
        [],
        ["72200000"],
        [],
        [],
        40,
        ["+25 CPV ciblé : 72200000"],
        "",
        "{}",
        importedAt);

    private static RiposteDbContext CreateContext() => new(
        new DbContextOptionsBuilder<RiposteDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
