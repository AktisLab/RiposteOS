using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Responses;
using RiposteOS.Core.Ai;
using System.ClientModel;

namespace RiposteOS.Infrastructure.Ai.Providers;

public sealed class OpenAiCompatibleChatClientFactory : IAiChatClientFactory
{
    public IChatClient Create(AiProvider provider)
    {
        if (provider.Protocol != AiProviderProtocol.OpenAiCompatible) throw new NotSupportedException("Le protocole IA n'est pas pris en charge.");
        var key = provider.ApiKeyEnvironmentVariableName is null
            ? "unused"
            : Environment.GetEnvironmentVariable(provider.ApiKeyEnvironmentVariableName)
                ?? throw new InvalidOperationException("La clé API configurée est introuvable.");
        var options = new OpenAIClientOptions { Endpoint = new Uri(provider.BaseUrl) };
        if ((provider.Capabilities & AiProviderCapabilities.Reasoning) != 0)
        {
#pragma warning disable OPENAI001 // The official Responses adapter is experimental in OpenAI 2.8, but it is the typed API that exposes reasoning summaries.
            return new ResponsesClient(provider.Model, new ApiKeyCredential(key), options).AsIChatClient();
#pragma warning restore OPENAI001
        }

        return new ChatClient(provider.Model, new ApiKeyCredential(key), options).AsIChatClient();
    }
}
