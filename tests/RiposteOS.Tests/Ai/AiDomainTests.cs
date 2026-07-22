using RiposteOS.Core.Ai;
using RiposteOS.Core.Consultations;

namespace RiposteOS.Tests.Ai;

public sealed class AiDomainTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 19, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ProviderNormalizesValuesAndCanBeUpdated()
    {
        var provider = new AiProvider("  Local  ", AiProviderProtocol.OpenAiCompatible, "http://localhost:11434/v1", "  model  ", "  AI_KEY  ", true, Now);

        provider.Update(" Remote ", AiProviderProtocol.OpenAiCompatible, "https://example.test/v1", " next ", null, false, Now.AddMinutes(1));

        Assert.Equal("Remote", provider.Name);
        Assert.Equal("https://example.test/v1", provider.BaseUrl);
        Assert.Equal("next", provider.Model);
        Assert.Null(provider.ApiKeyEnvironmentVariableName);
        Assert.False(provider.IsEnabled);
        Assert.Equal(AiProviderHealthStatus.Unknown, provider.HealthStatus);
        Assert.Null(provider.HealthCheckedAt);
        Assert.Equal(Now.AddMinutes(1), provider.UpdatedAt);
        Assert.Throws<ArgumentOutOfRangeException>(() => provider.Update("provider", (AiProviderProtocol)99, "https://example.test", "model", null, true, Now));
    }

    [Fact]
    public void ProviderRecordsOnlyResolvedHealthStates()
    {
        var provider = new AiProvider("Local", AiProviderProtocol.OpenAiCompatible, "http://localhost:11434/v1", "model", null, true, Now);

        provider.RecordHealthCheck(AiProviderHealthStatus.Available, Now.AddMinutes(1));

        Assert.Equal(AiProviderHealthStatus.Available, provider.HealthStatus);
        Assert.Equal(Now.AddMinutes(1), provider.HealthCheckedAt);
        Assert.Throws<ArgumentOutOfRangeException>(() => provider.RecordHealthCheck(AiProviderHealthStatus.Unknown, Now));
    }

    [Fact]
    public void ProviderTracksStoredApiKeyWithoutExposingPlaintextState()
    {
        var provider = new AiProvider("Remote", AiProviderProtocol.OpenAiCompatible, "https://example.test/v1", "model", null, true, Now);
        provider.RecordHealthCheck(AiProviderHealthStatus.Available, Now.AddMinutes(1));

        provider.SetEncryptedApiKey("encrypted-value", Now.AddMinutes(2));

        Assert.True(provider.HasStoredApiKey);
        Assert.Equal("encrypted-value", provider.EncryptedApiKey);
        Assert.Equal(AiProviderHealthStatus.Unknown, provider.HealthStatus);
        Assert.Null(provider.HealthCheckedAt);
        provider.ClearStoredApiKey(Now.AddMinutes(3));
        Assert.False(provider.HasStoredApiKey);
        Assert.Null(provider.EncryptedApiKey);
        Assert.Throws<ArgumentException>(() => provider.SetEncryptedApiKey(" ", Now));
    }

    [Theory]
    [InlineData("", "https://example.test", "model")]
    [InlineData("provider", "relative", "model")]
    [InlineData("provider", "ftp://example.test", "model")]
    [InlineData("provider", "https://example.test", "")]
    public void ProviderRejectsInvalidConfiguration(string name, string baseUrl, string model)
    {
        Assert.ThrowsAny<ArgumentException>(() => new AiProvider(name, AiProviderProtocol.OpenAiCompatible, baseUrl, model, null, true, Now));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AiProvider("provider", (AiProviderProtocol)99, "https://example.test", "model", null, true, Now));
    }

    [Fact]
    public void AssignmentValidatesAndRefreshesProvider()
    {
        var assignment = new AiTaskAssignment(AiTask.DocumentClassification, Guid.NewGuid(), Now);
        var providerId = Guid.NewGuid();

        assignment.Assign(providerId, Now.AddMinutes(1));

        Assert.Equal(providerId, assignment.ProviderId);
        Assert.Equal(Now.AddMinutes(1), assignment.UpdatedAt);
        Assert.Throws<ArgumentException>(() => assignment.Assign(Guid.Empty, Now));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AiTaskAssignment((AiTask)99, Guid.NewGuid(), Now));
    }

    [Fact]
    public void ClassificationCompletesWithProvenanceAndEvidence()
    {
        var classification = new ConsultationDocumentClassification(Guid.NewGuid(), Guid.NewGuid(), Now);
        var evidence = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };

        Assert.True(classification.TryStart(Now.AddMinutes(1)));
        Assert.False(classification.TryStart(Now.AddMinutes(2)));
        classification.Complete(ConsultationDocumentKind.Pricing, DocumentClassificationConfidence.High, [evidence[0], evidence[1], evidence[0]], Guid.NewGuid(), " Local ", " model ", Now.AddMinutes(2));

        Assert.Equal(DocumentClassificationStatus.Completed, classification.Status);
        Assert.Equal(ConsultationDocumentKind.Pricing, classification.ProposedKind);
        Assert.Equal(DocumentClassificationConfidence.High, classification.Confidence);
        Assert.Equal([evidence[0], evidence[1]], classification.EvidencePassageIds);
        Assert.Equal("Local", classification.ProviderName);
        Assert.Equal("model", classification.Model);
        Assert.Equal(Now.AddMinutes(2), classification.CompletedAt);
        Assert.Throws<InvalidOperationException>(() => classification.Fail("no", Now.AddMinutes(3)));
    }

    [Fact]
    public void ClassificationFailsAndRetriesOnlyFromRetryableStates()
    {
        var classification = new ConsultationDocumentClassification(Guid.NewGuid(), Guid.NewGuid(), Now);

        classification.Fail(" Configuration absente ", Now.AddMinutes(1), notConfigured: true);
        classification.Retry(Now.AddMinutes(2));

        Assert.Equal(DocumentClassificationStatus.Queued, classification.Status);
        Assert.Null(classification.FailedAt);
        Assert.Null(classification.ErrorMessage);
        Assert.Throws<InvalidOperationException>(() => classification.Retry(Now.AddMinutes(3)));
        Assert.Throws<ArgumentException>(() => classification.Fail(" ", Now.AddMinutes(3)));
    }

    [Fact]
    public void ClassificationRejectsInvalidCompletionData()
    {
        var classification = new ConsultationDocumentClassification(Guid.NewGuid(), Guid.NewGuid(), Now);
        classification.TryStart(Now);

        Assert.Throws<ArgumentException>(() => classification.Complete(ConsultationDocumentKind.Other, DocumentClassificationConfidence.Low, [], Guid.NewGuid(), "provider", "model", Now));
        Assert.Throws<ArgumentException>(() => classification.Complete(ConsultationDocumentKind.Other, DocumentClassificationConfidence.Low, [Guid.Empty], Guid.NewGuid(), "provider", "model", Now));
        Assert.Throws<ArgumentException>(() => classification.Complete(ConsultationDocumentKind.Other, DocumentClassificationConfidence.Low, [Guid.NewGuid()], Guid.Empty, "provider", "model", Now));
        Assert.Throws<ArgumentOutOfRangeException>(() => classification.Complete((ConsultationDocumentKind)99, DocumentClassificationConfidence.Low, [Guid.NewGuid()], Guid.NewGuid(), "provider", "model", Now));
    }

    [Fact]
    public void ClassificationRequiresBothIdentifiersAndARunningStateToComplete()
    {
        Assert.Throws<ArgumentException>(() => new ConsultationDocumentClassification(Guid.Empty, Guid.NewGuid(), Now));
        Assert.Throws<ArgumentException>(() => new ConsultationDocumentClassification(Guid.NewGuid(), Guid.Empty, Now));

        var classification = new ConsultationDocumentClassification(Guid.NewGuid(), Guid.NewGuid(), Now);
        Assert.Throws<InvalidOperationException>(() => classification.Complete(ConsultationDocumentKind.Other, DocumentClassificationConfidence.Low, [Guid.NewGuid()], Guid.NewGuid(), "provider", "model", Now));
    }
}
