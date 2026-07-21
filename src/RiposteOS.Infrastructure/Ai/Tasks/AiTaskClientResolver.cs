using Microsoft.EntityFrameworkCore;
using RiposteOS.Core.Ai;
using RiposteOS.Infrastructure.Ai.Providers;
using RiposteOS.Infrastructure.Persistence;
namespace RiposteOS.Infrastructure.Ai.Tasks;

public sealed class AiTaskClientResolver(RiposteDbContext dbContext, IAiChatClientFactory factory) : IAiTaskClientResolver
{
    public async Task<AiTaskClient?> ResolveAsync(AiTask task, CancellationToken cancellationToken)
    {
        var required = AiTaskCapabilities.RequiredBy(task);
        if ((required & AiProviderCapabilities.Chat) == 0) return null;
        var candidates = await (from assignment in dbContext.Set<AiTaskAssignment>().AsNoTracking()
                                join candidate in dbContext.Set<AiProvider>().AsNoTracking() on assignment.ProviderId equals candidate.Id
                                where assignment.Task == task && candidate.IsEnabled
                                select candidate).ToArrayAsync(cancellationToken);
        var provider = candidates.SingleOrDefault(candidate =>
            (candidate.Capabilities & required) == required);
        return provider is null ? null : new AiTaskClient(factory.Create(provider), provider.Id, provider.Name, provider.Model, provider.Capabilities);
    }
}
