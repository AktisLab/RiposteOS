using Microsoft.AspNetCore.DataProtection;

namespace RiposteOS.Infrastructure.Ai.Providers;

public sealed class AiProviderSecretProtector(IDataProtectionProvider dataProtectionProvider)
{
    private const int MaximumApiKeyLength = 4_096;
    private readonly IDataProtector _protector = dataProtectionProvider.CreateProtector("RiposteOS.Ai.ProviderApiKey.v1");

    public string Protect(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey) || apiKey.Length > MaximumApiKeyLength)
        {
            throw new ArgumentException("A valid API key is required.", nameof(apiKey));
        }

        return _protector.Protect(apiKey);
    }

    public string Unprotect(string encryptedApiKey) => _protector.Unprotect(encryptedApiKey);
}
