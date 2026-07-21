using Microsoft.Extensions.AI;
using RiposteOS.Core.Ai;

namespace RiposteOS.Infrastructure.Ai.Providers;

public interface IAiEmbeddingGeneratorFactory
{
    IEmbeddingGenerator<string, Embedding<float>> Create(AiProvider provider);
}
