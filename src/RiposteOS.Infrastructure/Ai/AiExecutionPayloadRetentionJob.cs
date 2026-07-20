using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RiposteOS.Core.Ai;
using RiposteOS.Infrastructure.Persistence;

namespace RiposteOS.Infrastructure.Ai;

public sealed class AiExecutionPayloadRetentionJob(
    RiposteDbContext dbContext,
    IOptions<AiExecutionPayloadRetentionOptions> options,
    TimeProvider timeProvider)
{
    public Task ExecuteAsync(CancellationToken cancellationToken) =>
        (from payload in dbContext.Set<AiExecutionPayload>()
         join execution in dbContext.Set<AiExecutionLog>() on payload.ExecutionId equals execution.Id
         where execution.StartedAt < timeProvider.GetUtcNow().AddDays(-options.Value.RetentionDays)
         select payload)
            .ExecuteDeleteAsync(cancellationToken);
}
