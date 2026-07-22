using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;
using RiposteOS.Core.Ai;
using System.ClientModel;
using System.ClientModel.Primitives;

namespace RiposteOS.Infrastructure.Ai.Providers;

public sealed class OpenAiCompatibleChatClientFactory(AiProviderApiKeyResolver apiKeyResolver) : IAiChatClientFactory
{
    public IChatClient Create(AiProvider provider)
    {
        if (provider.Protocol != AiProviderProtocol.OpenAiCompatible) throw new NotSupportedException("Le protocole IA n'est pas pris en charge.");
        var key = apiKeyResolver.Resolve(provider) ?? "unused";
        var options = new OpenAIClientOptions
        {
            Endpoint = new Uri(provider.BaseUrl),
            NetworkTimeout = TimeSpan.FromMinutes(9.5),
            RetryPolicy = new ClientRetryPolicy(0),
        };
        return new ChatClient(provider.Model, new ApiKeyCredential(key), options).AsIChatClient();
    }
}
