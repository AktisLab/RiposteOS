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
        await ReconcileAsync(source, cancellationToken);
        if (await dbContext.Set<ImportRun>()
                .Where(run => run.Source == source && ActiveStatuses.Contains(run.Status))
                .OrderByDescending(run => run.QueuedAt)
                .FirstOrDefaultAsync(cancellationToken) is { } activeRun)
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

    public async Task<int> ReconcileAsync(string source, CancellationToken cancellationToken)
    {
        source = SourcingSource.Normalize(source);
        var now = timeProvider.GetUtcNow();
        var staleBefore = now.AddHours(-2);
        var staleRuns = await dbContext.Set<ImportRun>()
            .Where(run => run.Source == source
                && ActiveStatuses.Contains(run.Status)
                && run.LastHeartbeatAt < staleBefore)
            .ToArrayAsync(cancellationToken);

        foreach (var run in staleRuns)
        {
            run.Fail("L'import a été interrompu avant sa fin.", now);
        }

        if (staleRuns.Length > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return staleRuns.Length;
    }

    public Task<DateTimeOffset?> GetLastSuccessfulAtAsync(
        string source,
        CancellationToken cancellationToken)
    {
        source = SourcingSource.Normalize(source);
        return dbContext.Set<ImportRun>()
            .AsNoTracking()
            .Where(run => run.Source == source && run.Status == ImportRunStatus.Succeeded)
            .OrderByDescending(run => run.FinishedAt)
            .Select(run => run.FinishedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> StartAsync(Guid runId, CancellationToken cancellationToken)
    {
        var startedAt = timeProvider.GetUtcNow();
        if (string.Equals(
                dbContext.Database.ProviderName,
                "Microsoft.EntityFrameworkCore.InMemory",
                StringComparison.Ordinal))
        {
            var run = await dbContext.Set<ImportRun>().SingleOrDefaultAsync(
                item => item.Id == runId && item.Status == ImportRunStatus.Queued,
                cancellationToken);
            if (run is null)
            {
                return false;
            }

            run.TryStart(startedAt);
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }

        dbContext.ChangeTracker.Clear();
        return await dbContext.Set<ImportRun>()
            .Where(run => run.Id == runId && run.Status == ImportRunStatus.Queued)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(run => run.Status, ImportRunStatus.Running)
                    .SetProperty(run => run.StartedAt, startedAt)
                    .SetProperty(run => run.LastHeartbeatAt, startedAt),
                cancellationToken) == 1;
    }

    public async Task AddProgressAsync(
        Guid runId,
        DateOnly publicationDate,
        int fetched,
        int created,
        int updated,
        int unchanged,
        int skipped,
        CancellationToken cancellationToken)
    {
        var run = await dbContext.Set<ImportRun>().SingleAsync(item => item.Id == runId, cancellationToken);
        run.RecordProgress(
            publicationDate,
            fetched,
            created,
            updated,
            unchanged,
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
