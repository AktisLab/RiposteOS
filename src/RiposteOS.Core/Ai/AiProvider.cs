namespace RiposteOS.Core.Ai;

public sealed class AiProvider
{
    public AiProvider(Guid id, string name, AiProviderProtocol protocol, string baseUrl, string model, string? apiKeyEnvironmentVariableName, bool isEnabled, DateTimeOffset createdAt, DateTimeOffset updatedAt, AiProviderCapabilities capabilities = AiProviderCapabilities.Chat, string? encryptedApiKey = null)
    {
        Id = id; Name = name; Protocol = protocol; BaseUrl = baseUrl; Model = model; ApiKeyEnvironmentVariableName = apiKeyEnvironmentVariableName; IsEnabled = isEnabled; CreatedAt = createdAt; UpdatedAt = updatedAt; Capabilities = ValidCapabilities(capabilities); EncryptedApiKey = encryptedApiKey;
    }
    public AiProvider(string name, AiProviderProtocol protocol, string baseUrl, string model, string? apiKeyEnvironmentVariableName, bool isEnabled, DateTimeOffset now, AiProviderCapabilities capabilities = AiProviderCapabilities.Chat)
    {
        Name = Required(name, 200, nameof(name));
        Protocol = Enum.IsDefined(protocol) ? protocol : throw new ArgumentOutOfRangeException(nameof(protocol));
        BaseUrl = AbsoluteUrl(baseUrl);
        Model = Required(model, 200, nameof(model));
        ApiKeyEnvironmentVariableName = Optional(apiKeyEnvironmentVariableName, 200, nameof(apiKeyEnvironmentVariableName));
        IsEnabled = isEnabled;
        Capabilities = ValidCapabilities(capabilities);
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public AiProviderProtocol Protocol { get; private set; }
    public string BaseUrl { get; private set; }
    public string Model { get; private set; }
    public string? ApiKeyEnvironmentVariableName { get; private set; }
    public string? EncryptedApiKey { get; private set; }
    public bool HasStoredApiKey => EncryptedApiKey is not null;
    public bool IsEnabled { get; private set; }
    public AiProviderCapabilities Capabilities { get; private set; }
    public AiProviderHealthStatus HealthStatus { get; private set; } = AiProviderHealthStatus.Unknown;
    public DateTimeOffset? HealthCheckedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public void Update(string name, AiProviderProtocol protocol, string baseUrl, string model, string? apiKeyEnvironmentVariableName, bool isEnabled, DateTimeOffset now, AiProviderCapabilities capabilities = AiProviderCapabilities.Chat)
    {
        Name = Required(name, 200, nameof(name)); Protocol = Enum.IsDefined(protocol) ? protocol : throw new ArgumentOutOfRangeException(nameof(protocol));
        BaseUrl = AbsoluteUrl(baseUrl); Model = Required(model, 200, nameof(model));
        ApiKeyEnvironmentVariableName = Optional(apiKeyEnvironmentVariableName, 200, nameof(apiKeyEnvironmentVariableName)); IsEnabled = isEnabled; Capabilities = ValidCapabilities(capabilities); UpdatedAt = now;
        HealthStatus = AiProviderHealthStatus.Unknown;
        HealthCheckedAt = null;
    }

    public void RecordHealthCheck(AiProviderHealthStatus status, DateTimeOffset checkedAt)
    {
        if (status is not (AiProviderHealthStatus.Available or AiProviderHealthStatus.Unavailable)) throw new ArgumentOutOfRangeException(nameof(status));
        HealthStatus = status;
        HealthCheckedAt = checkedAt;
    }

    public void SetEncryptedApiKey(string encryptedApiKey, DateTimeOffset now)
    {
        EncryptedApiKey = Required(encryptedApiKey, 8_000, nameof(encryptedApiKey));
        ResetHealth(now);
    }

    public void ClearStoredApiKey(DateTimeOffset now)
    {
        EncryptedApiKey = null;
        ResetHealth(now);
    }

    private void ResetHealth(DateTimeOffset now)
    {
        HealthStatus = AiProviderHealthStatus.Unknown;
        HealthCheckedAt = null;
        UpdatedAt = now;
    }

    private static string AbsoluteUrl(string value) => Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" ? uri.AbsoluteUri : throw new ArgumentException("An absolute HTTP URL is required.", nameof(value));
    private static string Required(string value, int max, string name) => string.IsNullOrWhiteSpace(value) || value.Trim().Length > max ? throw new ArgumentException("A valid value is required.", name) : value.Trim();
    private static string? Optional(string? value, int max, string name) => value is null ? null : Required(value, max, name);
    private static AiProviderCapabilities ValidCapabilities(AiProviderCapabilities value) => value is AiProviderCapabilities.None || (value & ~(AiProviderCapabilities.Chat | AiProviderCapabilities.Embedding | AiProviderCapabilities.ToolCalling | AiProviderCapabilities.Reasoning)) != 0 ? throw new ArgumentOutOfRangeException(nameof(value)) : value;
}
