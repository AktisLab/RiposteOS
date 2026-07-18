using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using RiposteOS.Core.Consultations;
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
        var existingPublications = await dbContext.Set<OpportunityPublication>()
            .Include(publication => publication.Opportunity)
            .Where(publication => publication.Source == source && sourceIds.Contains(publication.SourceId))
            .ToDictionaryAsync(publication => publication.SourceId, cancellationToken);
        var legacyOpportunities = await dbContext.Set<Opportunity>()
            .Where(opportunity => opportunity.Source == source && sourceIds.Contains(opportunity.SourceId))
            .ToDictionaryAsync(opportunity => opportunity.SourceId, cancellationToken);
        var eformsNoticeIds = uniqueOpportunities
            .Where(opportunity => opportunity.EformsNoticeId is not null)
            .Select(opportunity => opportunity.EformsNoticeId!.Value)
            .Distinct()
            .ToArray();
        var opportunitiesByEformsNoticeId = eformsNoticeIds.Length == 0
            ? []
            : await dbContext.Set<Opportunity>()
                .Where(opportunity => opportunity.EformsNoticeId != null
                    && eformsNoticeIds.Contains(opportunity.EformsNoticeId.Value))
                .ToDictionaryAsync(
                    opportunity => opportunity.EformsNoticeId!.Value,
                    cancellationToken);
        var references = uniqueOpportunities
            .SelectMany(opportunity => opportunity.References)
            .Distinct()
            .ToArray();
        var referenceSources = references.Select(reference => reference.Source).Distinct().ToArray();
        var referenceSourceIds = references.Select(reference => reference.SourceId).Distinct().ToArray();
        var referencedPublications = references.Length == 0
            ? []
            : await dbContext.Set<OpportunityPublication>()
                .Include(publication => publication.Opportunity)
                .Where(publication => referenceSources.Contains(publication.Source)
                    && referenceSourceIds.Contains(publication.SourceId))
                .ToArrayAsync(cancellationToken);
        var publicationsByReference = referencedPublications.ToDictionary(
            publication => (publication.Source, publication.SourceId));
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
            if (existingPublications.TryGetValue(sourceOpportunity.SourceId, out var publication))
            {
                var opportunity = publication.Opportunity
                    ?? throw new InvalidOperationException("The publication opportunity was not loaded.");
                var merged = false;
                if (sourceOpportunity.EformsNoticeId is { } existingIdentifier
                    && opportunitiesByEformsNoticeId.TryGetValue(existingIdentifier, out var existingIdentifiedOpportunity)
                    && existingIdentifiedOpportunity.Id != opportunity.Id)
                {
                    await MergeOpportunityAsync(
                        opportunity,
                        existingIdentifiedOpportunity,
                        cancellationToken);
                    opportunity = existingIdentifiedOpportunity;
                    merged = true;
                }

                opportunity.IdentifyByEformsNotice(sourceOpportunity.EformsNoticeId);
                var publicationChanged = publication.Refresh(
                    sourceOpportunity.NoticeUrl,
                    sourceOpportunity.DocumentUrl,
                    sourceOpportunity.RawPayload,
                    now);
                if (opportunity.Source != source || opportunity.SourceId != sourceOpportunity.SourceId)
                {
                    if (publicationChanged || merged)
                    {
                        changed++;
                    }
                    else
                    {
                        unchanged++;
                    }

                    continue;
                }

                var previousRevision = new OpportunityRevision(opportunity, now);
                var canonicalChanged = opportunity.RefreshFromSource(
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
                if (!canonicalChanged)
                {
                    if (opportunity.MatchScore != match.Score
                        || !opportunity.MatchReasons.SequenceEqual(match.Reasons, StringComparer.Ordinal))
                    {
                        opportunity.ReassessMatch(match.Score, match.Reasons, now);
                    }

                    if (publicationChanged)
                    {
                        changed++;
                    }
                    else
                    {
                        unchanged++;
                    }

                    continue;
                }

                dbContext.Set<OpportunityRevision>().Add(previousRevision);
                changed++;
                continue;
            }

            var referencedOpportunityIds = sourceOpportunity.References
                .Select(reference => publicationsByReference.GetValueOrDefault(
                    (reference.Source, reference.SourceId))?.OpportunityId)
                .Where(opportunityId => opportunityId is not null)
                .Select(opportunityId => opportunityId!.Value)
                .Distinct()
                .ToList();
            if (sourceOpportunity.EformsNoticeId is { } eformsNoticeId
                && opportunitiesByEformsNoticeId.TryGetValue(eformsNoticeId, out var identifiedOpportunity))
            {
                referencedOpportunityIds.Add(identifiedOpportunity.Id);
            }

            var matchedOpportunityIds = referencedOpportunityIds.Distinct().ToArray();
            if (matchedOpportunityIds.Length > 1)
            {
                throw new InvalidOperationException(
                    "The publication references several existing opportunities.");
            }

            Opportunity? referencedOpportunity = null;
            if (matchedOpportunityIds.Length == 1)
            {
                referencedOpportunity = opportunitiesByEformsNoticeId.Values
                    .Concat(referencedPublications.Select(item => item.Opportunity!))
                    .First(item => item.Id == matchedOpportunityIds[0]);
            }

            if (referencedOpportunity is not null)
            {
                var newPublication = referencedOpportunity.AddPublication(
                    source,
                    sourceOpportunity.SourceId,
                    sourceOpportunity.NoticeUrl,
                    sourceOpportunity.DocumentUrl,
                    sourceOpportunity.RawPayload,
                    now);
                dbContext.Set<OpportunityPublication>().Add(newPublication);
                changed++;
                continue;
            }

            if (legacyOpportunities.TryGetValue(sourceOpportunity.SourceId, out var legacyOpportunity))
            {
                var newPublication = legacyOpportunity.AddPublication(
                    source,
                    sourceOpportunity.SourceId,
                    sourceOpportunity.NoticeUrl,
                    sourceOpportunity.DocumentUrl,
                    sourceOpportunity.RawPayload,
                    now);
                dbContext.Set<OpportunityPublication>().Add(newPublication);
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
                sourceOpportunity.DocumentUrl,
                sourceOpportunity.EformsNoticeId);
            createdOpportunity.AddPublication(
                source,
                sourceOpportunity.SourceId,
                sourceOpportunity.NoticeUrl,
                sourceOpportunity.DocumentUrl,
                sourceOpportunity.RawPayload,
                now);
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

    private async Task MergeOpportunityAsync(
        Opportunity duplicate,
        Opportunity canonical,
        CancellationToken cancellationToken)
    {
        var duplicateConsultation = await dbContext.Set<Consultation>()
            .SingleOrDefaultAsync(
                consultation => consultation.OpportunityId == duplicate.Id,
                cancellationToken);
        if (duplicateConsultation is not null
            && await dbContext.Set<Consultation>().AnyAsync(
                consultation => consultation.OpportunityId == canonical.Id,
                cancellationToken))
        {
            throw new InvalidOperationException(
                "The duplicate and canonical opportunities both have a consultation.");
        }

        if (canonical.Status == OpportunityStatus.ToQualify)
        {
            if (duplicate.Status == OpportunityStatus.Retained)
            {
                canonical.Retain();
            }
            else if (duplicate.Status == OpportunityStatus.Dismissed)
            {
                canonical.Dismiss();
            }
        }
        else if (duplicate.Status != OpportunityStatus.ToQualify
            && duplicate.Status != canonical.Status)
        {
            throw new InvalidOperationException(
                "The duplicate eForms notice has conflicting qualification statuses.");
        }

        if (duplicateConsultation is not null)
        {
            duplicateConsultation.ReassignToOpportunity(
                canonical.Id,
                timeProvider.GetUtcNow());
            canonical.Retain();
        }

        var publications = await dbContext.Set<OpportunityPublication>()
            .Where(publication => publication.OpportunityId == duplicate.Id)
            .ToArrayAsync(cancellationToken);
        foreach (var publication in publications)
        {
            publication.ReassignTo(canonical);
        }

        var revisions = await dbContext.Set<OpportunityRevision>()
            .Where(revision => revision.OpportunityId == duplicate.Id)
            .ToArrayAsync(cancellationToken);
        foreach (var revision in revisions)
        {
            revision.ReassignTo(canonical);
        }

        dbContext.Set<Opportunity>().Remove(duplicate);
    }
}
