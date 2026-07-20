using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;
using RiposteOS.Core.Ai;
using System.ClientModel;

namespace RiposteOS.Infrastructure.Ai;

public sealed class OpenAiCompatibleChatClientFactory : IAiChatClientFactory
{
    public IChatClient Create(AiProvider provider)
    {
        if (provider.Protocol != AiProviderProtocol.OpenAiCompatible) throw new NotSupportedException("Le protocole IA n'est pas pris en charge.");
        var key = provider.ApiKeyEnvironmentVariableName is null
            ? "unused"
            : Environment.GetEnvironmentVariable(provider.ApiKeyEnvironmentVariableName)
                ?? throw new InvalidOperationException("La clé API configurée est introuvable.");
        return new ChatClient(provider.Model, new ApiKeyCredential(key), new OpenAIClientOptions { Endpoint = new Uri(provider.BaseUrl) }).AsIChatClient();
    }
}
