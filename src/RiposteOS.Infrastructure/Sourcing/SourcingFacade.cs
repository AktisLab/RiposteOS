using Hangfire;
using Gridify;
using Gridify.EntityFramework;
using Microsoft.EntityFrameworkCore;
using RiposteOS.Core.Sourcing;
using RiposteOS.Infrastructure.Persistence;

namespace RiposteOS.Infrastructure.Sourcing;

public sealed class SourcingFacade(
    RiposteDbContext dbContext,
    IEnumerable<IOpportunitySource> sources,
    SourcingSettingsStore settingsStore,
    ImportRunStore runStore,
    IBackgroundJobClient jobClient)
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
                ? "publicationDate desc,responseDeadline,id"
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

        return await settingsStore.UpdateAsync(profile, cancellationToken);
    }

    public async Task<Opportunity?> UpdateOpportunityStatusAsync(
        Guid id,
        OpportunityStatus status,
        CancellationToken cancellationToken)
    {
        var opportunity = await dbContext.Set<Opportunity>()
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (opportunity is null)
        {
            return null;
        }

        switch (status)
        {
            case OpportunityStatus.ToQualify:
                opportunity.ReturnToQualification();
                break;
            case OpportunityStatus.Retained:
                opportunity.Retain();
                break;
            case OpportunityStatus.Dismissed:
                opportunity.Dismiss();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(status));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return opportunity;
    }
}
