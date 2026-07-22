using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Embeddings;
using RiposteOS.Core.Ai;
using System.ClientModel;

namespace RiposteOS.Infrastructure.Ai.Providers;

public sealed class OpenAiCompatibleEmbeddingGeneratorFactory(AiProviderApiKeyResolver apiKeyResolver) : IAiEmbeddingGeneratorFactory
{
    public IEmbeddingGenerator<string, Embedding<float>> Create(AiProvider provider)
    {
        if (provider.Protocol != AiProviderProtocol.OpenAiCompatible) throw new NotSupportedException("Le protocole IA n'est pas pris en charge.");
        var key = apiKeyResolver.Resolve(provider) ?? "unused";
        return new EmbeddingClient(provider.Model, new ApiKeyCredential(key), new OpenAIClientOptions { Endpoint = new Uri(provider.BaseUrl) }).AsIEmbeddingGenerator();
    }
}
