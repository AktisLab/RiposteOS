using Microsoft.EntityFrameworkCore;
using Npgsql;
using RiposteOS.Core.Sourcing;
using RiposteOS.Infrastructure.Persistence;

namespace RiposteOS.Infrastructure.Sourcing;

public sealed class ImportRunStore(
    RiposteDbContext dbContext,
    TimeProvider timeProvider)
{
    private static readonly ImportRunStatus[] ActiveStatuses =
        [ImportRunStatus.Queued, ImportRunStatus.Running];

    public async Task<(ImportRun Run, bool Created)> QueueAsync(
        string source,
        CancellationToken cancellationToken)
    {
        source = SourcingSource.Normalize(source);
        var staleBefore = timeProvider.GetUtcNow().AddHours(-2);
        var activeRuns = await dbContext.Set<ImportRun>()
            .Where(run => run.Source == source && ActiveStatuses.Contains(run.Status))
            .OrderBy(run => run.QueuedAt)
            .ToArrayAsync(cancellationToken);

        foreach (var staleRun in activeRuns.Where(run => run.LastHeartbeatAt < staleBefore))
        {
            staleRun.Fail("L'import a été interrompu avant sa fin.", timeProvider.GetUtcNow());
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        if (activeRuns.LastOrDefault(run => ActiveStatuses.Contains(run.Status)) is { } activeRun)
        {
            return (activeRun, false);
        }

        var run = new ImportRun(source, timeProvider.GetUtcNow());
        dbContext.Set<ImportRun>().Add(run);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return (run, true);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            dbContext.Entry(run).State = EntityState.Detached;
            var concurrentRun = await dbContext.Set<ImportRun>()
                .AsNoTracking()
                .SingleAsync(
                    item => item.Source == source && ActiveStatuses.Contains(item.Status),
                    cancellationToken);
            return (concurrentRun, false);
        }
    }

    public async Task<bool> StartAsync(Guid runId, CancellationToken cancellationToken)
    {
        var run = await dbContext.Set<ImportRun>().SingleAsync(item => item.Id == runId, cancellationToken);
        if (!run.TryStart(timeProvider.GetUtcNow()))
        {
            return false;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task AddProgressAsync(
        Guid runId,
        DateOnly publicationDate,
        int fetched,
        int created,
        int updated,
        int skipped,
        CancellationToken cancellationToken)
    {
        var run = await dbContext.Set<ImportRun>().SingleAsync(item => item.Id == runId, cancellationToken);
        run.RecordProgress(
            publicationDate,
            fetched,
            created,
            updated,
            skipped,
            timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task CompleteAsync(Guid runId, CancellationToken cancellationToken)
    {
        var run = await dbContext.Set<ImportRun>().SingleAsync(item => item.Id == runId, cancellationToken);
        run.Complete(timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task FailAsync(Guid runId, string message, CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();
        var run = await dbContext.Set<ImportRun>().SingleOrDefaultAsync(item => item.Id == runId, cancellationToken);
        if (run is null || !ActiveStatuses.Contains(run.Status))
        {
            return;
        }

        run.Fail(message, timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
        };
}
