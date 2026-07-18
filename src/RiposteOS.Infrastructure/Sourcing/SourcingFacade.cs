using Hangfire;
using Gridify;
using Gridify.EntityFramework;
using Microsoft.EntityFrameworkCore;
using RiposteOS.Core.Consultations;
using RiposteOS.Core.Sourcing;
using RiposteOS.Infrastructure.Persistence;

namespace RiposteOS.Infrastructure.Sourcing;

public sealed class SourcingFacade(
    RiposteDbContext dbContext,
    IEnumerable<IOpportunitySource> sources,
    SourcingSettingsStore settingsStore,
    ImportRunStore runStore,
    OpportunityImporter importer,
    IBackgroundJobClient jobClient,
    IRecurringJobManager recurringJobs)
{
    private static readonly OpportunityGridifyMapper OpportunityMapper = new();
    private static readonly ImportRunGridifyMapper ImportRunMapper = new();

    public async Task<OpportunityPageResult> ListOpportunitiesAsync(
        int page,
        int pageSize,
        string? filter,
        string? orderBy,
        string[] departments,
        string? cpv,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(pageSize, 100);

        var query = new GridifyQuery(
            page,
            pageSize,
            filter,
            string.IsNullOrWhiteSpace(orderBy)
                ? "matchScore desc,publicationDate desc,id"
                : $"{orderBy},id");
        if (!query.IsValid(OpportunityMapper))
        {
            return new OpportunityPageResult(
                [],
                0,
                ["Le filtre ou le tri demandé est invalide."]);
        }

        var opportunities = dbContext.Set<Opportunity>().AsNoTracking();
        if (string.Equals(
                dbContext.Database.ProviderName,
                "Microsoft.EntityFrameworkCore.InMemory",
                StringComparison.Ordinal))
        {
            var inMemoryOpportunities = await opportunities.ToListAsync(cancellationToken);
            var inMemoryResult = inMemoryOpportunities
                .Where(opportunity => departments.Length == 0 ||
                    opportunity.DepartmentCodes.Any(departments.Contains))
                .Where(opportunity => string.IsNullOrWhiteSpace(cpv) ||
                    opportunity.CpvCodes.Any(code =>
                        code.StartsWith(cpv, StringComparison.Ordinal)))
                .AsQueryable()
                .Gridify(query, OpportunityMapper);

            return new OpportunityPageResult(
                inMemoryResult.Data.ToArray(),
                inMemoryResult.Count,
                []);
        }

        if (departments.Length > 0)
        {
            opportunities = opportunities.Where(opportunity =>
                departments.Any(code =>
                    EF.Property<string[]>(opportunity, "_departmentCodes").Contains(code)));
        }

        if (!string.IsNullOrWhiteSpace(cpv))
        {
            opportunities = opportunities.Where(opportunity =>
                EF.Property<string[]>(opportunity, "_cpvCodes")
                    .Any(code => EF.Functions.Like(code, cpv + "%")));
        }

        var result = await opportunities
            .GridifyAsync(query, cancellationToken, OpportunityMapper);

        return new OpportunityPageResult(result.Data.ToArray(), result.Count, []);
    }

    public async Task<ImportQueueResult?> QueueImportAsync(
        string sourceKey,
        CancellationToken cancellationToken)
    {
        var source = sources.SingleOrDefault(item =>
            string.Equals(item.Key, sourceKey, StringComparison.OrdinalIgnoreCase));
        if (source is null)
        {
            return null;
        }

        if (await settingsStore.GetAsync(cancellationToken) is null)
        {
            return new ImportQueueResult(null, false);
        }

        var (run, created) = await runStore.QueueAsync(source.Key, cancellationToken);
        if (!created)
        {
            return new ImportQueueResult(run, false);
        }

        try
        {
            var command = new ImportOpportunities(source.Key, run.Id);
            jobClient.Enqueue<SourcingImportJob>(job =>
                job.ExecuteAsync(command, CancellationToken.None));
        }
        catch
        {
            await runStore.FailAsync(
                run.Id,
                "L'import n'a pas pu être transmis au worker.",
                CancellationToken.None);
            throw;
        }

        return new ImportQueueResult(run, true);
    }

    public async Task<ImportRunPageResult> ListImportsAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(pageSize, 100);

        var result = await dbContext.Set<ImportRun>()
            .AsNoTracking()
            .GridifyAsync(
                new GridifyQuery(page, pageSize, null, "queuedAt desc,id"),
                cancellationToken,
                ImportRunMapper);

        return new ImportRunPageResult(result.Data.ToArray(), result.Count);
    }

    public Task<ImportRun?> GetImportAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Set<ImportRun>()
            .AsNoTracking()
            .SingleOrDefaultAsync(run => run.Id == id, cancellationToken);

    public async Task<ImportIssuePageResult> ListImportIssuesAsync(
        Guid runId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(pageSize, 100);

        var query = dbContext.Set<ImportIssue>()
            .AsNoTracking()
            .Where(issue => issue.RunId == runId);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(issue => issue.CreatedAt)
            .ThenBy(issue => issue.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToArrayAsync(cancellationToken);
        return new ImportIssuePageResult(items, totalCount);
    }

    public Task<ImportIssueRetryResult?> RetryImportIssueAsync(
        Guid issueId,
        CancellationToken cancellationToken) =>
        importer.RetryIssueAsync(issueId, cancellationToken);

    public Task<SourcingSettings?> GetSettingsAsync(CancellationToken cancellationToken) =>
        settingsStore.GetAsync(cancellationToken);

    public async Task<SourcingSettings?> UpdateSettingsAsync(
        SourcingProfile profile,
        CancellationToken cancellationToken)
    {
        var importIsActive = await dbContext.Set<ImportRun>()
            .AsNoTracking()
            .AnyAsync(
                run => run.Status == ImportRunStatus.Queued
                    || run.Status == ImportRunStatus.Running,
                cancellationToken);
        if (importIsActive)
        {
            return null;
        }

        var settings = await settingsStore.UpdateAsync(profile, cancellationToken);
        SourcingRecurringJobRegistrar.Register(
            recurringJobs,
            sources,
            settings,
            SourcingSettings.DefaultSynchronizationCron);
        return settings;
    }

    public async Task<OpportunityStatusUpdateResult> UpdateOpportunityStatusAsync(
        Guid id,
        OpportunityStatus status,
        CancellationToken cancellationToken)
    {
        if (status == OpportunityStatus.Retained)
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }

        await using var transaction = dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;
        var opportunity = await GetOpportunityForUpdateAsync(id, cancellationToken);
        if (opportunity is null)
        {
            return new OpportunityStatusUpdateResult(null, false);
        }

        if (await dbContext.Set<Consultation>()
            .AsNoTracking()
            .AnyAsync(consultation => consultation.OpportunityId == id, cancellationToken))
        {
            return new OpportunityStatusUpdateResult(opportunity, true);
        }

        switch (status)
        {
            case OpportunityStatus.ToQualify:
                opportunity.ReturnToQualification();
                break;
            case OpportunityStatus.Dismissed:
                opportunity.Dismiss();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(status));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return new OpportunityStatusUpdateResult(opportunity, false);
    }

    private Task<Opportunity?> GetOpportunityForUpdateAsync(
        Guid id,
        CancellationToken cancellationToken) =>
        dbContext.Database.IsNpgsql()
            ? dbContext.Set<Opportunity>()
                .FromSqlInterpolated($"SELECT * FROM sourcing.opportunities WHERE \"Id\" = {id} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken)
            : dbContext.Set<Opportunity>()
                .SingleOrDefaultAsync(opportunity => opportunity.Id == id, cancellationToken);
}
