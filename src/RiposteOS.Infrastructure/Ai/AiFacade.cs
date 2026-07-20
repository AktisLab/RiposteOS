using Gridify;
using Gridify.EntityFramework;
using Microsoft.EntityFrameworkCore;
using RiposteOS.Core.Ai;
using RiposteOS.Infrastructure.Persistence;

namespace RiposteOS.Infrastructure.Ai;

public sealed class AiFacade(
    RiposteDbContext dbContext,
    IAiProviderHealthChecker healthChecker,
    TimeProvider timeProvider)
{
    private static readonly AiExecutionLogGridifyMapper ExecutionLogMapper = new();

    public Task<AiProvider[]> ListProvidersAsync(CancellationToken ct) => dbContext.Set<AiProvider>().AsNoTracking().OrderBy(x => x.Name).ToArrayAsync(ct);
    public async Task<AiProvider> CreateProviderAsync(string name, AiProviderProtocol protocol, string baseUrl, string model, string? apiKeyEnvironmentVariableName, bool isEnabled, CancellationToken ct) { var provider = new AiProvider(name, protocol, baseUrl, model, apiKeyEnvironmentVariableName, isEnabled, timeProvider.GetUtcNow()); dbContext.Set<AiProvider>().Add(provider); await dbContext.SaveChangesAsync(ct); return provider; }
    public async Task<AiProvider?> UpdateProviderAsync(Guid id, string name, AiProviderProtocol protocol, string baseUrl, string model, string? apiKeyEnvironmentVariableName, bool isEnabled, CancellationToken ct) { var p = await dbContext.Set<AiProvider>().SingleOrDefaultAsync(x => x.Id == id, ct); if (p is null) return null; p.Update(name, protocol, baseUrl, model, apiKeyEnvironmentVariableName, isEnabled, timeProvider.GetUtcNow()); await dbContext.SaveChangesAsync(ct); return p; }
    public async Task<bool> DeleteProviderAsync(Guid id, CancellationToken ct) { if (await dbContext.Set<AiTaskAssignment>().AnyAsync(x => x.ProviderId == id, ct) || await dbContext.Set<ConsultationDocumentClassification>().AnyAsync(x => x.ProviderId == id, ct)) return false; var p = await dbContext.Set<AiProvider>().SingleOrDefaultAsync(x => x.Id == id, ct); if (p is null) return false; dbContext.Remove(p); await dbContext.SaveChangesAsync(ct); return true; }
    public async Task<bool> AssignAsync(AiTask task, Guid providerId, CancellationToken ct) { var provider = await dbContext.Set<AiProvider>().SingleOrDefaultAsync(x => x.Id == providerId && x.IsEnabled, ct); if (provider is null) return false; var assignment = await dbContext.Set<AiTaskAssignment>().SingleOrDefaultAsync(x => x.Task == task, ct); if (assignment is null) dbContext.Set<AiTaskAssignment>().Add(new AiTaskAssignment(task, providerId, timeProvider.GetUtcNow())); else assignment.Assign(providerId, timeProvider.GetUtcNow()); await dbContext.SaveChangesAsync(ct); return true; }
    public Task<AiTaskAssignment?> GetAssignmentAsync(AiTask task, CancellationToken ct) => dbContext.Set<AiTaskAssignment>().AsNoTracking().SingleOrDefaultAsync(x => x.Task == task, ct);

    public async Task<AiExecutionLogPageResult> ListExecutionLogsAsync(
        int page,
        int pageSize,
        string? filter,
        string? orderBy,
        CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(pageSize, 100);

        var query = new GridifyQuery(
            page,
            pageSize,
            filter,
            string.IsNullOrWhiteSpace(orderBy) ? "startedAt desc,id" : $"{orderBy},id");
        if (!query.IsValid(ExecutionLogMapper))
        {
            return new AiExecutionLogPageResult([], 0, ["Le filtre ou le tri demandé est invalide."]);
        }

        var logs = dbContext.Set<AiExecutionLog>().AsNoTracking();
        if (string.Equals(dbContext.Database.ProviderName, "Microsoft.EntityFrameworkCore.InMemory", StringComparison.Ordinal))
        {
            var result = (await logs.ToListAsync(ct)).AsQueryable().Gridify(query, ExecutionLogMapper);
            return new AiExecutionLogPageResult(result.Data.ToArray(), result.Count, []);
        }

        var pageResult = await logs.GridifyAsync(query, ct, ExecutionLogMapper);
        return new AiExecutionLogPageResult(pageResult.Data.ToArray(), pageResult.Count, []);
    }

    public async Task<AiExecutionLogDetailsResult?> GetExecutionLogAsync(Guid id, CancellationToken ct)
    {
        var execution = await dbContext.Set<AiExecutionLog>().AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, ct);
        if (execution is null)
        {
            return null;
        }

        var payload = await dbContext.Set<AiExecutionPayload>().AsNoTracking().SingleOrDefaultAsync(item => item.ExecutionId == id, ct);
        return new AiExecutionLogDetailsResult(execution, payload?.Input, payload?.Output);
    }

    public async Task<bool?> TestProviderAsync(Guid id, CancellationToken ct)
    {
        var provider = await dbContext.Set<AiProvider>().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (provider is null)
        {
            return null;
        }

        var status = await CheckAsync(provider, ct);
        provider.RecordHealthCheck(status, timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(ct);
        return status == AiProviderHealthStatus.Available;
    }

    public async Task RefreshEnabledProviderHealthAsync(CancellationToken ct)
    {
        var providers = await dbContext.Set<AiProvider>().Where(provider => provider.IsEnabled).ToArrayAsync(ct);
        foreach (var provider in providers)
        {
            var status = await CheckAsync(provider, ct);
            provider.RecordHealthCheck(status, timeProvider.GetUtcNow());
            await dbContext.SaveChangesAsync(ct);
        }
    }

    private async Task<AiProviderHealthStatus> CheckAsync(AiProvider provider, CancellationToken ct)
    {
        try
        {
            return await healthChecker.CheckAsync(provider, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return AiProviderHealthStatus.Unavailable;
        }
    }
}

public sealed record AiExecutionLogPageResult(AiExecutionLog[] Items, int TotalCount, string[] ValidationErrors);
public sealed record AiExecutionLogDetailsResult(AiExecutionLog Execution, string? Input, string? Output);
