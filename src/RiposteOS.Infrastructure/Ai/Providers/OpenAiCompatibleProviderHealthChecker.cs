using System.Net.Http.Headers;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using RiposteOS.Core.Ai;

namespace RiposteOS.Infrastructure.Ai.Providers;

public sealed class OpenAiCompatibleProviderHealthChecker(
    HttpClient client,
    IAiChatClientFactory? chatClientFactory = null,
    ILoggerFactory? loggerFactory = null) : IAiProviderHealthChecker
{
    public async Task<AiProviderHealthStatus> CheckAsync(AiProvider provider, CancellationToken cancellationToken)
    {
        if (provider.Protocol != AiProviderProtocol.OpenAiCompatible)
        {
            return AiProviderHealthStatus.Unavailable;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{provider.BaseUrl.TrimEnd('/')}/models");
            if (provider.ApiKeyEnvironmentVariableName is { } environmentVariableName)
            {
                var apiKey = Environment.GetEnvironmentVariable(environmentVariableName);
                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    return AiProviderHealthStatus.Unavailable;
                }

                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            }

            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            return response.IsSuccessStatusCode
                ? AiProviderHealthStatus.Available
                : AiProviderHealthStatus.Unavailable;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return AiProviderHealthStatus.Unavailable;
        }
    }

    public async Task<AiProviderHealthStatus> TestAsync(AiProvider provider, CancellationToken cancellationToken)
    {
        var status = await CheckAsync(provider, cancellationToken);
        if (status != AiProviderHealthStatus.Available || (provider.Capabilities & AiProviderCapabilities.ToolCalling) == 0) return status;
        if (chatClientFactory is null) return AiProviderHealthStatus.Unavailable;

        var invoked = false;
        string Probe()
        {
            invoked = true;
            return "ok";
        }

        try
        {
            var tool = AIFunctionFactory.Create((Func<string>)Probe, "health_probe", "Vérifie l'appel de fonction du provider.");
            using var chat = chatClientFactory.Create(provider).AsBuilder().UseFunctionInvocation(loggerFactory).Build();
            var answer = string.Empty;
            await foreach (var update in chat.GetStreamingResponseAsync(
                               [new ChatMessage(ChatRole.User, "Appelle health_probe puis réponds uniquement OK.")],
                               new ChatOptions { Tools = [tool], ToolMode = ChatToolMode.RequireSpecific("health_probe") },
                               cancellationToken))
            {
                answer += update.Text;
            }

            return invoked && !string.IsNullOrWhiteSpace(answer) ? AiProviderHealthStatus.Available : AiProviderHealthStatus.Unavailable;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return AiProviderHealthStatus.Unavailable;
        }
    }
}
