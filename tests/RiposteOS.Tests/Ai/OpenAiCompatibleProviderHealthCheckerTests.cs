using System.Net;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using RiposteOS.Core.Ai;
using RiposteOS.Infrastructure.Ai;
using RiposteOS.Infrastructure.Ai.Providers;
using RiposteOS.Tests.TestSupport;

namespace RiposteOS.Tests.Ai;

public sealed class OpenAiCompatibleProviderHealthCheckerTests
{
    [Fact]
    public async Task ChecksModelsEndpointWithoutGeneratingContent()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK);
        var checker = new OpenAiCompatibleProviderHealthChecker(new HttpClient(handler), TestAiSecrets.CreateResolver());
        var provider = Provider(apiKeyEnvironmentVariableName: null);

        var status = await checker.CheckAsync(provider, CancellationToken.None);

        Assert.Equal(AiProviderHealthStatus.Available, status);
        Assert.Equal(HttpMethod.Get, handler.Method);
        Assert.Equal("http://provider.test/v1/models", handler.RequestUri!.ToString());
        Assert.Null(handler.Authorization);
    }

    [Fact]
    public async Task ReturnsUnavailableForHttpFailureMissingSecretAndTransportFailure()
    {
        var failedChecker = new OpenAiCompatibleProviderHealthChecker(new HttpClient(new RecordingHandler(HttpStatusCode.BadGateway)), TestAiSecrets.CreateResolver());
        var missingSecretChecker = new OpenAiCompatibleProviderHealthChecker(new HttpClient(new RecordingHandler(HttpStatusCode.OK)), TestAiSecrets.CreateResolver());
        var throwingChecker = new OpenAiCompatibleProviderHealthChecker(new HttpClient(new ThrowingHandler()), TestAiSecrets.CreateResolver());
        var provider = Provider(apiKeyEnvironmentVariableName: null);

        Assert.Equal(AiProviderHealthStatus.Unavailable, await failedChecker.CheckAsync(provider, CancellationToken.None));
        Assert.Equal(AiProviderHealthStatus.Unavailable, await missingSecretChecker.CheckAsync(Provider("RIPOSTEOS_MISSING_HEALTH_SECRET"), CancellationToken.None));
        Assert.Equal(AiProviderHealthStatus.Unavailable, await throwingChecker.CheckAsync(provider, CancellationToken.None));
        Assert.Equal(
            AiProviderHealthStatus.Unavailable,
            await failedChecker.CheckAsync(
                new AiProvider(Guid.NewGuid(), "provider", (AiProviderProtocol)99, "http://provider.test/v1", "model", null, true, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow),
                CancellationToken.None));
    }

    [Fact]
    public async Task SendsConfiguredSecretAndRethrowsRequestedCancellation()
    {
        const string secretName = "RIPOSTEOS_HEALTH_CHECKER_TEST_KEY";
        Environment.SetEnvironmentVariable(secretName, "health-secret");
        try
        {
            var handler = new RecordingHandler(HttpStatusCode.OK);
            var checker = new OpenAiCompatibleProviderHealthChecker(new HttpClient(handler), TestAiSecrets.CreateResolver());
            var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            Assert.Equal(AiProviderHealthStatus.Available, await checker.CheckAsync(Provider(secretName), CancellationToken.None));
            Assert.Equal(new AuthenticationHeaderValue("Bearer", "health-secret"), handler.Authorization);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => checker.CheckAsync(Provider(), cancellation.Token));
        }
        finally
        {
            Environment.SetEnvironmentVariable(secretName, null);
        }
    }

    [Fact]
    public async Task ManualTestProbesTheDeclaredToolCallingContract()
    {
        var checker = new OpenAiCompatibleProviderHealthChecker(
            new HttpClient(new RecordingHandler(HttpStatusCode.OK)),
            TestAiSecrets.CreateResolver(),
            new ProbeChatFactory(),
            NullLoggerFactory.Instance);
        var provider = new AiProvider("provider", AiProviderProtocol.OpenAiCompatible, "http://provider.test/v1", "model", null, true, DateTimeOffset.UtcNow, AiProviderCapabilities.Chat | AiProviderCapabilities.ToolCalling);

        Assert.Equal(AiProviderHealthStatus.Available, await checker.TestAsync(provider, CancellationToken.None));
        Assert.Equal(AiProviderHealthStatus.Unavailable, await new OpenAiCompatibleProviderHealthChecker(new HttpClient(new RecordingHandler(HttpStatusCode.OK)), TestAiSecrets.CreateResolver()).TestAsync(provider, CancellationToken.None));
    }

    private static AiProvider Provider(string? apiKeyEnvironmentVariableName = null) =>
        new("provider", AiProviderProtocol.OpenAiCompatible, "http://provider.test/v1", "model", apiKeyEnvironmentVariableName, true, DateTimeOffset.UtcNow);

    private sealed class RecordingHandler(HttpStatusCode statusCode) : HttpMessageHandler
    {
        public HttpMethod? Method { get; private set; }
        public Uri? RequestUri { get; private set; }
        public AuthenticationHeaderValue? Authorization { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Method = request.Method;
            RequestUri = request.RequestUri;
            Authorization = request.Headers.Authorization;
            return Task.FromResult(new HttpResponseMessage(statusCode));
        }
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException();
    }

    private sealed class ProbeChatFactory : IAiChatClientFactory
    {
        public IChatClient Create(AiProvider provider) => new ProbeChatClient();
    }

    private sealed class ProbeChatClient : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            if (messages.SelectMany(message => message.Contents).OfType<FunctionResultContent>().Any())
            {
                yield return new ChatResponseUpdate(ChatRole.Assistant, "OK");
                yield break;
            }

            yield return new ChatResponseUpdate(ChatRole.Assistant, [new FunctionCallContent("probe", "health_probe", new Dictionary<string, object?>())]);
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }
}
