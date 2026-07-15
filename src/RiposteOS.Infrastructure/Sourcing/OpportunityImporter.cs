using Microsoft.EntityFrameworkCore;
using RiposteOS.Core.Sourcing;
using RiposteOS.Infrastructure.Persistence;

namespace RiposteOS.Infrastructure.Sourcing;

public sealed class OpportunityImporter(
    IEnumerable<IOpportunitySource> sources,
    RiposteDbContext dbContext,
    TimeProvider timeProvider,
    SourcingSettingsStore settingsStore,
    ImportRunStore runStore)
{
    public async Task ImportAsync(
        ImportOpportunities command,
        CancellationToken cancellationToken)
    {
        var source = GetSource(command.Source);
        var settings = await settingsStore.GetAsync(cancellationToken)
            ?? throw new InvalidOperationException(
                "A sourcing profile is required before an import can run.");
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var syncState = await dbContext.Set<SourcingSyncState>()
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Source == source.Key, cancellationToken);
        var startDate = source.GetStartDate(today, syncState?.LastSuccessfulPublicationDate);

        await foreach (var page in source.ReadPagesAsync(settings, startDate, today, cancellationToken))
        {
            var (created, updated) = await UpsertAsync(
                source.Key,
                page.Opportunities,
                settings,
                cancellationToken);
            await runStore.AddProgressAsync(
                command.RunId,
                page.PublicationDate,
                page.Fetched,
                created,
                updated,
                page.Skipped,
                cancellationToken);
            dbContext.ChangeTracker.Clear();
        }

        var state = await dbContext.Set<SourcingSyncState>()
            .SingleOrDefaultAsync(item => item.Source == source.Key, cancellationToken)
            ?? new SourcingSyncState(source.Key);
        if (dbContext.Entry(state).State == EntityState.Detached)
        {
            dbContext.Set<SourcingSyncState>().Add(state);
        }

        state.Advance(today, timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private IOpportunitySource GetSource(string key) =>
        sources.SingleOrDefault(source =>
            string.Equals(source.Key, key, StringComparison.OrdinalIgnoreCase))
        ?? throw new NotSupportedException($"Sourcing source '{key}' is not registered.");

    private async Task<(int Created, int Updated)> UpsertAsync(
        string source,
        IReadOnlyList<SourceOpportunity> opportunities,
        SourcingSettings settings,
        CancellationToken cancellationToken)
    {
        var uniqueOpportunities = opportunities
            .GroupBy(opportunity => opportunity.SourceId, StringComparer.Ordinal)
            .Select(group => group.Last())
            .ToArray();
        var sourceIds = uniqueOpportunities.Select(opportunity => opportunity.SourceId).ToArray();
        var existing = await dbContext.Set<Opportunity>()
            .Where(opportunity => opportunity.Source == source && sourceIds.Contains(opportunity.SourceId))
            .ToDictionaryAsync(opportunity => opportunity.SourceId, cancellationToken);
        var now = timeProvider.GetUtcNow();
        var created = 0;
        var updated = 0;

        foreach (var sourceOpportunity in uniqueOpportunities)
        {
            var match = SourcingMatcher.Evaluate(
                settings,
                sourceOpportunity.Title,
                sourceOpportunity.DepartmentCodes,
                sourceOpportunity.CpvCodes,
                sourceOpportunity.DescriptorLabels,
                sourceOpportunity.ResponseDeadline,
                now);
            if (existing.TryGetValue(sourceOpportunity.SourceId, out var opportunity))
            {
                opportunity.RefreshFromSource(
                    sourceOpportunity.Title,
                    sourceOpportunity.Buyer,
                    sourceOpportunity.PublicationDate,
                    sourceOpportunity.ResponseDeadline,
                    sourceOpportunity.DepartmentCodes,
                    sourceOpportunity.CpvCodes,
                    sourceOpportunity.DescriptorCodes,
                    sourceOpportunity.DescriptorLabels,
                    match.Score,
                    match.Reasons,
                    sourceOpportunity.NoticeUrl,
                    sourceOpportunity.RawPayload,
                    now);
                updated++;
                continue;
            }

            dbContext.Set<Opportunity>().Add(new Opportunity(
                source,
                sourceOpportunity.SourceId,
                sourceOpportunity.Title,
                sourceOpportunity.Buyer,
                sourceOpportunity.PublicationDate,
                sourceOpportunity.ResponseDeadline,
                sourceOpportunity.DepartmentCodes,
                sourceOpportunity.CpvCodes,
                sourceOpportunity.DescriptorCodes,
                sourceOpportunity.DescriptorLabels,
                match.Score,
                match.Reasons,
                sourceOpportunity.NoticeUrl,
                sourceOpportunity.RawPayload,
                now));
            created++;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return (created, updated);
    }
}
