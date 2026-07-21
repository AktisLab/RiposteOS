using Microsoft.Extensions.AI;

namespace RiposteOS.Infrastructure.Ai.Tasks;

public interface IAiEmbeddingTaskResolver
{
    Task<AiEmbeddingTaskClient?> ResolveAsync(CancellationToken cancellationToken);
}

public sealed record AiEmbeddingTaskClient(
    IEmbeddingGenerator<string, Embedding<float>> Generator,
    Guid ProviderId,
    string ProviderName,
    string Model);
