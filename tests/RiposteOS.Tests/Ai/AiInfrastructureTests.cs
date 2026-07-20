using Microsoft.EntityFrameworkCore;
using RiposteOS.Core.Ai;
using RiposteOS.Infrastructure.Ai;
using RiposteOS.Infrastructure.Persistence;

namespace RiposteOS.Tests.Ai;

public sealed class AiInfrastructureTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 19, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ClassificationStoreQueuesRetriesAndKeepsActiveClassification()
    {
        await using var dbContext = CreateDbContext();
        var consultationId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var store = new DocumentClassificationStore(dbContext, new FixedTimeProvider(Now));

        var created = await store.QueueAsync(consultationId, documentId, CancellationToken.None);
        await dbContext.SaveChangesAsync();
        created.Classification.Fail("failure", Now.AddMinutes(1));
        await dbContext.SaveChangesAsync();
        var retried = await new DocumentClassificationStore(dbContext, new FixedTimeProvider(Now.AddMinutes(2))).QueueAsync(consultationId, documentId, CancellationToken.None);
        var active = await store.QueueAsync(consultationId, documentId, CancellationToken.None);

        Assert.True(created.Enqueue);
        Assert.True(retried.Enqueue);
        Assert.False(active.Enqueue);
        Assert.Same(retried.Classification, active.Classification);
        Assert.Equal(DocumentClassificationStatus.Queued, active.Classification.Status);
    }

    [Fact]
    public async Task ResolverReturnsOnlyEnabledAssignedProvider()
    {
        await using var dbContext = CreateDbContext();
        var enabled = Provider(isEnabled: true);
        var disabled = Provider(isEnabled: false);
        dbContext.AddRange(enabled, disabled);
        dbContext.Add(new AiTaskAssignment(AiTask.DocumentClassification, enabled.Id, Now));
        await dbContext.SaveChangesAsync();
        var resolver = new AiTaskClientResolver(dbContext, new NullFactory());

        var resolved = await resolver.ResolveAsync(AiTask.DocumentClassification, CancellationToken.None);

        Assert.NotNull(resolved);
        Assert.Equal(enabled.Id, resolved.ProviderId);
        Assert.Equal(enabled.Name, resolved.ProviderName);
        Assert.Equal(enabled.Model, resolved.Model);

        enabled.Update(enabled.Name, enabled.Protocol, enabled.BaseUrl, enabled.Model, null, false, Now.AddMinutes(1));
        await dbContext.SaveChangesAsync();
        Assert.Null(await resolver.ResolveAsync(AiTask.DocumentClassification, CancellationToken.None));
    }

    [Fact]
    public async Task FacadeCreatesUpdatesAssignsAndRefusesUnsafeDeletion()
    {
        await using var dbContext = CreateDbContext();
        var facade = new AiFacade(dbContext, new FixedHealthChecker(AiProviderHealthStatus.Available), new FixedTimeProvider(Now));

        var provider = await facade.CreateProviderAsync(" Local ", AiProviderProtocol.OpenAiCompatible, "http://localhost:11434/v1", " model ", null, true, CancellationToken.None);
        var assigned = await facade.AssignAsync(AiTask.DocumentClassification, provider.Id, CancellationToken.None);
        var updated = await facade.UpdateProviderAsync(provider.Id, " Remote ", AiProviderProtocol.OpenAiCompatible, "https://example.test/v1", "other", null, false, CancellationToken.None);

        Assert.True(assigned);
        Assert.NotNull(updated);
        Assert.Equal("Remote", updated.Name);
        Assert.False(updated.IsEnabled);
        Assert.Equal(provider.Id, (await facade.GetAssignmentAsync(AiTask.DocumentClassification, CancellationToken.None))?.ProviderId);
        Assert.False(await facade.DeleteProviderAsync(provider.Id, CancellationToken.None));
        Assert.False(await facade.AssignAsync(AiTask.DocumentClassification, provider.Id, CancellationToken.None));
        Assert.Null(await facade.UpdateProviderAsync(Guid.NewGuid(), "x", AiProviderProtocol.OpenAiCompatible, "https://example.test", "m", null, true, CancellationToken.None));

        var removable = await facade.CreateProviderAsync("Removable", AiProviderProtocol.OpenAiCompatible, "https://example.test", "model", null, true, CancellationToken.None);
        Assert.True(await facade.DeleteProviderAsync(removable.Id, CancellationToken.None));
        Assert.False(await facade.DeleteProviderAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task FacadeProviderTestDistinguishesMissingAndFailedProviders()
    {
        await using var dbContext = CreateDbContext();
        var facade = new AiFacade(dbContext, new FixedHealthChecker(AiProviderHealthStatus.Unavailable), new FixedTimeProvider(Now));
        var provider = await facade.CreateProviderAsync("Local", AiProviderProtocol.OpenAiCompatible, "http://localhost:11434/v1", "model", null, true, CancellationToken.None);

        Assert.Null(await facade.TestProviderAsync(Guid.NewGuid(), CancellationToken.None));
        Assert.False(await facade.TestProviderAsync(provider.Id, CancellationToken.None));
        Assert.Equal(AiProviderHealthStatus.Unavailable, provider.HealthStatus);
        Assert.Equal(Now, provider.HealthCheckedAt);
    }

    [Fact]
    public async Task FacadeReassignsAnExistingTask()
    {
        await using var dbContext = CreateDbContext();
        var facade = new AiFacade(dbContext, new FixedHealthChecker(AiProviderHealthStatus.Available), new FixedTimeProvider(Now));
        var first = await facade.CreateProviderAsync("First", AiProviderProtocol.OpenAiCompatible, "https://first.example.test", "model", null, true, CancellationToken.None);
        var second = await facade.CreateProviderAsync("Second", AiProviderProtocol.OpenAiCompatible, "https://second.example.test", "model", null, true, CancellationToken.None);

        Assert.True(await facade.AssignAsync(AiTask.DocumentClassification, first.Id, CancellationToken.None));
        Assert.True(await facade.AssignAsync(AiTask.DocumentClassification, second.Id, CancellationToken.None));

        Assert.Equal(second.Id, (await facade.GetAssignmentAsync(AiTask.DocumentClassification, CancellationToken.None))?.ProviderId);
    }

    [Fact]
    public async Task ExecutionJournalSupportsEmptyPayloadsAndRejectsInvalidQueries()
    {
        await using var dbContext = CreateDbContext();
        var facade = new AiFacade(dbContext, new FixedHealthChecker(AiProviderHealthStatus.Available), new FixedTimeProvider(Now));
        var execution = new AiExecutionLog(
            AiExecutionOperation.DocumentClassification,
            new AiExecutionSubject(AiExecutionSubjectKind.Document, Guid.NewGuid(), "ccap.docx"),
            Guid.NewGuid(),
            "Local",
            "model",
            Guid.NewGuid(),
            Now);
        dbContext.Add(execution);
        await dbContext.SaveChangesAsync();

        var page = await facade.ListExecutionLogsAsync(1, 10, null, null, CancellationToken.None);
        var invalid = await facade.ListExecutionLogsAsync(1, 10, "unknown=value", null, CancellationToken.None);
        var details = await facade.GetExecutionLogAsync(execution.Id, CancellationToken.None);
        var missing = await facade.GetExecutionLogAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Single(page.Items);
        Assert.Empty(page.ValidationErrors);
        Assert.Empty(invalid.Items);
        Assert.NotEmpty(invalid.ValidationErrors);
        Assert.NotNull(details);
        Assert.Null(details.Input);
        Assert.Null(details.Output);
        Assert.Null(missing);
    }

    [Fact]
    public async Task ExecutionRecorderIgnoresMissingAndFinishedExecutionsWhenFailing()
    {
        await using var dbContext = CreateDbContext();
        var execution = new AiExecutionLog(
            AiExecutionOperation.DocumentAnalysis,
            new AiExecutionSubject(AiExecutionSubjectKind.Document, Guid.NewGuid(), "rc.docx"),
            Guid.NewGuid(),
            "Docling",
            null,
            null,
            Now);
        execution.Complete(Now.AddSeconds(1));
        dbContext.Add(execution);
        await dbContext.SaveChangesAsync();
        var recorder = new AiExecutionRecorder(dbContext, new FixedTimeProvider(Now));

        await recorder.FailAsync(Guid.NewGuid(), "ignored", false, CancellationToken.None);
        await recorder.FailAsync(execution.Id, "ignored", false, CancellationToken.None);

        Assert.Equal(AiExecutionStatus.Completed, execution.Status);
    }

    [Fact]
    public void OpenAiCompatibleFactoryRejectsUnsupportedProtocolAndMissingConfiguredSecret()
    {
        var factory = new OpenAiCompatibleChatClientFactory();
        var provider = Provider(isEnabled: true, apiKeyEnvironmentVariableName: "RIPOSTEOS_TEST_MISSING_KEY");

        Assert.Throws<InvalidOperationException>(() => factory.Create(provider));
        Assert.Throws<NotSupportedException>(() => factory.Create(new AiProvider(Guid.NewGuid(), "name", (AiProviderProtocol)99, "https://example.test", "model", null, true, Now, Now)));
        Assert.NotNull(factory.Create(Provider(isEnabled: true)));
    }

    [Fact]
    public async Task HealthRefreshChecksOnlyEnabledProviders()
    {
        await using var dbContext = CreateDbContext();
        var enabled = Provider(isEnabled: true);
        var disabled = Provider(isEnabled: false);
        dbContext.AddRange(enabled, disabled);
        await dbContext.SaveChangesAsync();
        var checker = new FixedHealthChecker(AiProviderHealthStatus.Available);
        var facade = new AiFacade(dbContext, checker, new FixedTimeProvider(Now));

        await new AiProviderHealthCheckJob(facade).ExecuteAsync(CancellationToken.None);

        Assert.Equal([enabled.Id], checker.CheckedProviderIds);
        Assert.Equal(AiProviderHealthStatus.Available, enabled.HealthStatus);
        Assert.Equal(Now, enabled.HealthCheckedAt);
        Assert.Equal(AiProviderHealthStatus.Unknown, disabled.HealthStatus);
    }

    [Fact]
    public async Task HealthCheckPersistsFailureAndRespectsCancellation()
    {
        await using var dbContext = CreateDbContext();
        var provider = Provider(isEnabled: true);
        dbContext.Add(provider);
        await dbContext.SaveChangesAsync();
        var facade = new AiFacade(dbContext, new ThrowingHealthChecker(), new FixedTimeProvider(Now));

        await facade.RefreshEnabledProviderHealthAsync(CancellationToken.None);

        Assert.Equal(AiProviderHealthStatus.Unavailable, provider.HealthStatus);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            facade.RefreshEnabledProviderHealthAsync(new CancellationToken(canceled: true)));
    }

    private static AiProvider Provider(bool isEnabled, string? apiKeyEnvironmentVariableName = null) =>
        new(Guid.NewGuid(), "provider", AiProviderProtocol.OpenAiCompatible, "https://example.test/v1", "model", apiKeyEnvironmentVariableName, isEnabled, Now, Now);

    private static RiposteDbContext CreateDbContext() => new(new DbContextOptionsBuilder<RiposteDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .Options);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class NullFactory : IAiChatClientFactory
    {
        public Microsoft.Extensions.AI.IChatClient Create(AiProvider provider) => null!;
    }

    private sealed class FixedHealthChecker(AiProviderHealthStatus status) : IAiProviderHealthChecker
    {
        public List<Guid> CheckedProviderIds { get; } = [];

        public Task<AiProviderHealthStatus> CheckAsync(AiProvider provider, CancellationToken cancellationToken)
        {
            CheckedProviderIds.Add(provider.Id);
            return Task.FromResult(status);
        }
    }

    private sealed class ThrowingHealthChecker : IAiProviderHealthChecker
    {
        public Task<AiProviderHealthStatus> CheckAsync(AiProvider provider, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new InvalidOperationException();
        }
    }
}
