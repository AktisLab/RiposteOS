using RiposteOS.Core.Ai;

namespace RiposteOS.Tests.Ai;

public sealed class AiExecutionLogTests
{
    private static readonly DateTimeOffset StartedAt = new(2026, 7, 20, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ExecutionKeepsItsGenericSubjectAndCorrelation()
    {
        var correlationId = Guid.NewGuid();
        var log = Create(AiExecutionOperation.DocumentAnalysis, correlationId);

        Assert.Equal(AiExecutionStatus.Running, log.Status);
        Assert.Equal(AiExecutionSubjectKind.Document, log.SubjectKind);
        Assert.Equal("document.pdf", log.SubjectLabel);
        Assert.Equal(correlationId, log.CorrelationId);
        Assert.Equal("Docling", log.ProviderName);
        Assert.Null(log.Model);
    }

    [Fact]
    public void SubjectValidatesItsOwnInvariant()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new AiExecutionSubject((AiExecutionSubjectKind)99, Guid.NewGuid(), "document.pdf"));
        Assert.Throws<ArgumentException>(() => new AiExecutionSubject(AiExecutionSubjectKind.Document, Guid.Empty, "document.pdf"));
        Assert.Throws<ArgumentException>(() => new AiExecutionSubject(AiExecutionSubjectKind.Document, Guid.NewGuid(), " "));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AiExecutionSubject(AiExecutionSubjectKind.Document, Guid.NewGuid(), new string('a', AiExecutionSubject.MaximumLabelLength + 1)));
    }

    [Fact]
    public void InvalidInputIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Create((AiExecutionOperation)99, Guid.NewGuid()));
    }

    [Fact]
    public void ProviderAndCompletionAreCapturedOnce()
    {
        var log = Create(AiExecutionOperation.DocumentClassification, Guid.NewGuid());
        var providerId = Guid.NewGuid();

        log.SetProvider(providerId, " Local ", " model ");
        log.Complete(StartedAt.AddSeconds(2));

        Assert.Equal(providerId, log.ProviderId);
        Assert.Equal("Local", log.ProviderName);
        Assert.Equal("model", log.Model);
        Assert.Equal(AiExecutionStatus.Completed, log.Status);
        Assert.Equal(StartedAt.AddSeconds(2), log.CompletedAt);
        Assert.Null(log.ErrorMessage);
        Assert.Throws<InvalidOperationException>(() => log.Complete(StartedAt.AddSeconds(3)));
    }

    [Fact]
    public void FailuresRemainSafeAndChronological()
    {
        var log = Create(AiExecutionOperation.DocumentAnalysis, Guid.NewGuid());

        Assert.Throws<ArgumentOutOfRangeException>(() => log.Fail("Erreur", StartedAt.AddSeconds(-1)));
        Assert.Throws<ArgumentException>(() => log.SetProvider(Guid.Empty, "provider", "model"));
        Assert.Throws<ArgumentException>(() => log.SetProvider(Guid.NewGuid(), " ", "model"));
        Assert.Throws<ArgumentOutOfRangeException>(() => log.SetProvider(Guid.NewGuid(), "provider", new string('a', AiExecutionLog.MaximumModelLength + 1)));

        log.Fail(" Échec sûr ", StartedAt.AddSeconds(1), true);

        Assert.Equal(AiExecutionStatus.NotConfigured, log.Status);
        Assert.Equal("Échec sûr", log.ErrorMessage);
        Assert.Equal(StartedAt.AddSeconds(1), log.FailedAt);
        Assert.Throws<InvalidOperationException>(() => log.Fail("Autre", StartedAt.AddSeconds(2)));
    }

    private static AiExecutionLog Create(
        AiExecutionOperation operation,
        Guid? correlationId) =>
        new(
            operation,
            new AiExecutionSubject(AiExecutionSubjectKind.Document, Guid.NewGuid(), "document.pdf"),
            correlationId,
            operation == AiExecutionOperation.DocumentAnalysis ? "Docling" : null,
            null,
            null,
            StartedAt);
}
