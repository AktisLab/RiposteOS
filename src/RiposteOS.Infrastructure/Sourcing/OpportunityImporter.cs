using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
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
            var (created, changed, unchanged) = await UpsertAsync(
                source.Key,
                page.Opportunities,
                settings,
                cancellationToken);
            foreach (var issue in page.Issues ?? [])
            {
                dbContext.Set<ImportIssue>().Add(new ImportIssue(
                    command.RunId,
                    source.Key,
                    issue.SourceId,
                    issue.ErrorCode,
                    issue.RawPayload,
                    timeProvider.GetUtcNow()));
            }

            await runStore.AddProgressAsync(
                command.RunId,
                page.PublicationDate,
                page.Fetched,
                created,
                changed,
                unchanged,
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

    public async Task<ImportIssueRetryResult?> RetryIssueAsync(
        Guid issueId,
        CancellationToken cancellationToken)
    {
        var issue = await dbContext.Set<ImportIssue>()
            .SingleOrDefaultAsync(item => item.Id == issueId, cancellationToken);
        if (issue is null)
        {
            return null;
        }

        if (issue.ResolvedAt is not null)
        {
            return new ImportIssueRetryResult(issue, true);
        }

        var source = GetSource(issue.Source);
        SourceOpportunity opportunity;
        try
        {
            opportunity = source.ParseRawOpportunity(issue.RawPayload);
        }
        catch (JsonException)
        {
            return new ImportIssueRetryResult(issue, false);
        }
        catch (FormatException)
        {
            return new ImportIssueRetryResult(issue, false);
        }

        var settings = await settingsStore.GetAsync(cancellationToken)
            ?? throw new InvalidOperationException(
                "A sourcing profile is required before an import issue can be retried.");
        await UpsertAsync(source.Key, [opportunity], settings, cancellationToken);
        issue.Resolve(timeProvider.GetUtcNow());
        dbContext.Set<ImportIssue>().Update(issue);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new ImportIssueRetryResult(issue, true);
    }

    private IOpportunitySource GetSource(string key) =>
        sources.SingleOrDefault(source =>
            string.Equals(source.Key, key, StringComparison.OrdinalIgnoreCase))
        ?? throw new NotSupportedException($"Sourcing source '{key}' is not registered.");

    private async Task<(int Created, int Changed, int Unchanged)> UpsertAsync(
        string source,
        IReadOnlyList<SourceOpportunity> opportunities,
        SourcingSettings settings,
        CancellationToken cancellationToken,
        bool retryOnConflict = true)
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
        var changed = 0;
        var unchanged = 0;

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
                var previousRevision = new OpportunityRevision(opportunity, now);
                if (!opportunity.RefreshFromSource(
                    sourceOpportunity.Title,
                    sourceOpportunity.Buyer,
                    sourceOpportunity.PublicationDate,
                    sourceOpportunity.ResponseDeadline,
                    sourceOpportunity.CountryCodes,
                    sourceOpportunity.DepartmentCodes,
                    sourceOpportunity.CpvCodes,
                    sourceOpportunity.DescriptorCodes,
                    sourceOpportunity.DescriptorLabels,
                    match.Score,
                    match.Reasons,
                    sourceOpportunity.NoticeUrl,
                    sourceOpportunity.RawPayload,
                    now,
                    sourceOpportunity.Description,
                    sourceOpportunity.ProcedureType,
                    sourceOpportunity.ContractNature,
                    sourceOpportunity.EstimatedValue,
                    sourceOpportunity.Currency,
                    sourceOpportunity.ExecutionDuration,
                    sourceOpportunity.DocumentUrl))
                {
                    if (opportunity.MatchScore != match.Score
                        || !opportunity.MatchReasons.SequenceEqual(match.Reasons, StringComparer.Ordinal))
                    {
                        opportunity.ReassessMatch(match.Score, match.Reasons, now);
                    }

                    unchanged++;
                    continue;
                }

                dbContext.Set<OpportunityRevision>().Add(previousRevision);
                changed++;
                continue;
            }

            var createdOpportunity = new Opportunity(
                source,
                sourceOpportunity.SourceId,
                sourceOpportunity.Title,
                sourceOpportunity.Buyer,
                sourceOpportunity.PublicationDate,
                sourceOpportunity.ResponseDeadline,
                sourceOpportunity.CountryCodes,
                sourceOpportunity.DepartmentCodes,
                sourceOpportunity.CpvCodes,
                sourceOpportunity.DescriptorCodes,
                sourceOpportunity.DescriptorLabels,
                match.Score,
                match.Reasons,
                sourceOpportunity.NoticeUrl,
                sourceOpportunity.RawPayload,
                now,
                sourceOpportunity.Description,
                sourceOpportunity.ProcedureType,
                sourceOpportunity.ContractNature,
                sourceOpportunity.EstimatedValue,
                sourceOpportunity.Currency,
                sourceOpportunity.ExecutionDuration,
                sourceOpportunity.DocumentUrl);
            dbContext.Set<Opportunity>().Add(createdOpportunity);
            created++;
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return (created, changed, unchanged);
        }
        catch (DbUpdateException exception) when (
            retryOnConflict
            && exception.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
            })
        {
            dbContext.ChangeTracker.Clear();
            return await UpsertAsync(
                source,
                opportunities,
                settings,
                cancellationToken,
                retryOnConflict: false);
        }
    }
}
