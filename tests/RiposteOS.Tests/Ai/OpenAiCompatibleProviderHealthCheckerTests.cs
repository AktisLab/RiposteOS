using System.Net;
using System.Net.Http.Headers;
using RiposteOS.Core.Ai;
using RiposteOS.Infrastructure.Ai;

namespace RiposteOS.Tests.Ai;

public sealed class OpenAiCompatibleProviderHealthCheckerTests
{
    [Fact]
    public async Task ChecksModelsEndpointWithoutGeneratingContent()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK);
        var checker = new OpenAiCompatibleProviderHealthChecker(new HttpClient(handler));
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
        var failedChecker = new OpenAiCompatibleProviderHealthChecker(new HttpClient(new RecordingHandler(HttpStatusCode.BadGateway)));
        var missingSecretChecker = new OpenAiCompatibleProviderHealthChecker(new HttpClient(new RecordingHandler(HttpStatusCode.OK)));
        var throwingChecker = new OpenAiCompatibleProviderHealthChecker(new HttpClient(new ThrowingHandler()));
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
            var checker = new OpenAiCompatibleProviderHealthChecker(new HttpClient(handler));
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
}
