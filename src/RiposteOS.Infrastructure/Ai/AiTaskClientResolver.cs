using Microsoft.EntityFrameworkCore;
using RiposteOS.Core.Ai;
using RiposteOS.Infrastructure.Persistence;
namespace RiposteOS.Infrastructure.Ai;

public sealed class AiTaskClientResolver(RiposteDbContext dbContext, IAiChatClientFactory factory) : IAiTaskClientResolver
{
    public async Task<AiTaskClient?> ResolveAsync(AiTask task, CancellationToken cancellationToken)
    {
        var provider = await (from assignment in dbContext.Set<AiTaskAssignment>().AsNoTracking()
                              join candidate in dbContext.Set<AiProvider>().AsNoTracking() on assignment.ProviderId equals candidate.Id
                              where assignment.Task == task && candidate.IsEnabled
                              select candidate).SingleOrDefaultAsync(cancellationToken);
        return provider is null ? null : new AiTaskClient(factory.Create(provider), provider.Id, provider.Name, provider.Model);
    }
}
