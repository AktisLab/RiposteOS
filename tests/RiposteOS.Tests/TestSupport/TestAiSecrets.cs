using Microsoft.AspNetCore.DataProtection;
using RiposteOS.Infrastructure.Ai.Providers;

namespace RiposteOS.Tests.TestSupport;

public static class TestAiSecrets
{
    public static AiProviderSecretProtector CreateProtector() =>
        new(new EphemeralDataProtectionProvider());

    public static AiProviderApiKeyResolver CreateResolver() =>
        new(CreateProtector());
}
