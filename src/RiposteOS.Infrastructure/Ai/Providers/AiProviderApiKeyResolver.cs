using RiposteOS.Core.Ai;

namespace RiposteOS.Infrastructure.Ai.Providers;

public sealed class AiProviderApiKeyResolver(AiProviderSecretProtector secretProtector)
{
    public string? Resolve(AiProvider provider)
    {
        if (provider.EncryptedApiKey is { } encryptedApiKey)
        {
            return secretProtector.Unprotect(encryptedApiKey);
        }

        if (provider.ApiKeyEnvironmentVariableName is null)
        {
            return null;
        }

        return Environment.GetEnvironmentVariable(provider.ApiKeyEnvironmentVariableName)
            ?? throw new InvalidOperationException("La clé API configurée est introuvable.");
    }
}
