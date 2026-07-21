using Microsoft.EntityFrameworkCore;
using RiposteOS.Core.Ai;
using RiposteOS.Infrastructure.Ai.Providers;
using RiposteOS.Infrastructure.Persistence;

namespace RiposteOS.Infrastructure.Ai.Tasks;

public sealed class AiEmbeddingTaskResolver(RiposteDbContext dbContext, IAiEmbeddingGeneratorFactory factory) : IAiEmbeddingTaskResolver
{
    public async Task<AiEmbeddingTaskClient?> ResolveAsync(CancellationToken cancellationToken)
    {
        var candidates = await (from assignment in dbContext.Set<AiTaskAssignment>().AsNoTracking()
                                join candidate in dbContext.Set<AiProvider>().AsNoTracking() on assignment.ProviderId equals candidate.Id
                                where assignment.Task == AiTask.DocumentEmbedding && candidate.IsEnabled
                                select candidate).ToArrayAsync(cancellationToken);
        var provider = candidates.SingleOrDefault(candidate =>
            (candidate.Capabilities & AiProviderCapabilities.Embedding) != 0);
        return provider is null ? null : new AiEmbeddingTaskClient(factory.Create(provider), provider.Id, provider.Name, provider.Model);
    }
}
