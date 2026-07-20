using Microsoft.EntityFrameworkCore;
using RiposteOS.Core.Ai;
using RiposteOS.Infrastructure.Persistence;

namespace RiposteOS.Infrastructure.Ai;

public sealed class AiExecutionRecorder(RiposteDbContext dbContext, TimeProvider timeProvider)
{
    public async Task<Guid> StartAsync(AiExecutionStart start, CancellationToken cancellationToken)
    {
        var execution = new AiExecutionLog(
            start.Operation,
            start.Subject,
            start.CorrelationId,
            start.ProviderName,
            start.Model,
            start.ProviderId,
            timeProvider.GetUtcNow());
        dbContext.Set<AiExecutionLog>().Add(execution);
        await dbContext.SaveChangesAsync(cancellationToken);
        return execution.Id;
    }

    public async Task SetProviderAsync(Guid id, Guid providerId, string providerName, string model, CancellationToken cancellationToken)
    {
        var execution = await dbContext.Set<AiExecutionLog>().SingleAsync(item => item.Id == id, cancellationToken);
        execution.SetProvider(providerId, providerName, model);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RecordInputAsync(Guid id, string input, CancellationToken cancellationToken)
    {
        dbContext.Set<AiExecutionPayload>().Add(new AiExecutionPayload(id, input));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RecordOutputAsync(Guid id, string output, CancellationToken cancellationToken)
    {
        var payload = await dbContext.Set<AiExecutionPayload>().SingleAsync(item => item.ExecutionId == id, cancellationToken);
        payload.RecordOutput(output);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task CompleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var execution = await dbContext.Set<AiExecutionLog>().SingleAsync(item => item.Id == id, cancellationToken);
        execution.Complete(timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task FailAsync(Guid id, string message, bool notConfigured, CancellationToken cancellationToken)
    {
        var execution = await dbContext.Set<AiExecutionLog>().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (execution is null || execution.Status != AiExecutionStatus.Running)
        {
            return;
        }

        execution.Fail(message, timeProvider.GetUtcNow(), notConfigured);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
