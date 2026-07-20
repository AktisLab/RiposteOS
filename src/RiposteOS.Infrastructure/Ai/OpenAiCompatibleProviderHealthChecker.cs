using System.Net.Http.Headers;
using RiposteOS.Core.Ai;

namespace RiposteOS.Infrastructure.Ai;

public sealed class OpenAiCompatibleProviderHealthChecker(HttpClient client) : IAiProviderHealthChecker
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
}
