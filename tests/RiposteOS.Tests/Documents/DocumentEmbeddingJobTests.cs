using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using RiposteOS.Core.Documents;
using RiposteOS.Infrastructure.Ai;
using RiposteOS.Infrastructure.Ai.Execution;
using RiposteOS.Infrastructure.Ai.Tasks;
using RiposteOS.Infrastructure.Documents;
using RiposteOS.Infrastructure.Persistence;

namespace RiposteOS.Tests.Documents;

public sealed class DocumentEmbeddingJobTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 20, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task IndexesCompletedPassagesOnceForTheConfiguredProfile()
    {
        await using var dbContext = CreateDbContext();
        var run = await SeedRunAsync(dbContext, "Le délai est le 12 septembre.");
        var generator = new FixedEmbeddingGenerator(DocumentPassageEmbedding.ExpectedDimension);
        var job = CreateJob(dbContext, new ResolvedEmbeddingResolver(generator));

        await job.ExecuteAsync(run.Id, CancellationToken.None);
        await job.ExecuteAsync(run.Id, CancellationToken.None);

        var embedding = Assert.Single(await dbContext.Set<DocumentPassageEmbedding>().ToArrayAsync());
        Assert.Equal(DocumentPassageEmbeddingStatus.Completed, embedding.Status);
        Assert.Equal(1, generator.CallCount);
        Assert.Equal(DocumentPassageEmbedding.ExpectedDimension, embedding.Dimension);
    }

    [Fact]
    public async Task PersistsActionableFailureWhenEmbeddingIsNotConfigured()
    {
        await using var dbContext = CreateDbContext();
        var run = await SeedRunAsync(dbContext, "Texte à indexer.");

        await CreateJob(dbContext, new NoEmbeddingResolver()).ExecuteAsync(run.Id, CancellationToken.None);

        var embedding = Assert.Single(await dbContext.Set<DocumentPassageEmbedding>().ToArrayAsync());
        Assert.Equal(DocumentPassageEmbeddingStatus.Failed, embedding.Status);
        Assert.Equal("L'indexation IA n'est pas configurée.", embedding.ErrorMessage);
    }

    [Fact]
    public async Task PersistsFailureWhenTheProviderReturnsTheWrongDimension()
    {
        await using var dbContext = CreateDbContext();
        var run = await SeedRunAsync(dbContext, "Texte à indexer.");

        await CreateJob(dbContext, new ResolvedEmbeddingResolver(new FixedEmbeddingGenerator(1))).ExecuteAsync(run.Id, CancellationToken.None);

        var embedding = Assert.Single(await dbContext.Set<DocumentPassageEmbedding>().ToArrayAsync());
        Assert.Equal(DocumentPassageEmbeddingStatus.Failed, embedding.Status);
        Assert.Equal("L'indexation des passages a échoué. Réessayez.", embedding.ErrorMessage);
    }

    [Fact]
    public async Task PersistsARetriableFailureWhenTheEmbeddingProviderCannotBeResolved()
    {
        await using var dbContext = CreateDbContext();
        var run = await SeedRunAsync(dbContext, "Texte à indexer.");

        await CreateJob(dbContext, new ThrowingEmbeddingResolver()).ExecuteAsync(run.Id, CancellationToken.None);

        var embedding = Assert.Single(await dbContext.Set<DocumentPassageEmbedding>().ToArrayAsync());
        Assert.Equal(DocumentPassageEmbeddingStatus.Failed, embedding.Status);
        Assert.Equal("L'indexation des passages a échoué. Réessayez.", embedding.ErrorMessage);
    }

    [Fact]
    public async Task ReplacesTheDerivedEmbeddingWhenAnExplicitRetryUsesAnotherProfile()
    {
        await using var dbContext = CreateDbContext();
        var run = await SeedRunAsync(dbContext, "Texte à indexer.");
        await CreateJob(dbContext, new ResolvedEmbeddingResolver(new FixedEmbeddingGenerator(DocumentPassageEmbedding.ExpectedDimension), "premier")).ExecuteAsync(run.Id, CancellationToken.None);

        await CreateJob(dbContext, new ResolvedEmbeddingResolver(new FixedEmbeddingGenerator(DocumentPassageEmbedding.ExpectedDimension), "second")).ExecuteAsync(run.Id, CancellationToken.None);

        var embedding = Assert.Single(await dbContext.Set<DocumentPassageEmbedding>().ToArrayAsync());
        Assert.Equal("second", embedding.Model);
        Assert.Equal(DocumentPassageEmbeddingStatus.Completed, embedding.Status);
    }

    [Fact]
    public async Task DoesNothingWhenTheCompletedRunHasNoPassage()
    {
        await using var dbContext = CreateDbContext();
        var document = new StoredDocument(Guid.NewGuid(), "empty.pdf", "application/pdf", 1, new string('a', 64), Now);
        var run = new DocumentProcessingRun(document.Id, Now);
        dbContext.AddRange(document, run);
        await dbContext.SaveChangesAsync();
        run.TryStart(Now);
        run.Complete(0, 0, Now);
        await dbContext.SaveChangesAsync();

        await CreateJob(dbContext, new ResolvedEmbeddingResolver(new FixedEmbeddingGenerator(DocumentPassageEmbedding.ExpectedDimension))).ExecuteAsync(run.Id, CancellationToken.None);

        Assert.Empty(await dbContext.Set<DocumentPassageEmbedding>().ToArrayAsync());
    }

    [Fact]
    public async Task MarksTheRunningEmbeddingFailedWhenTheRequestIsCancelled()
    {
        await using var dbContext = CreateDbContext();
        var run = await SeedRunAsync(dbContext, "Texte à indexer.");
        using var cancellation = new CancellationTokenSource();
        var job = CreateJob(dbContext, new ResolvedEmbeddingResolver(new CancellingEmbeddingGenerator(cancellation)));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => job.ExecuteAsync(run.Id, cancellation.Token));

        var embedding = Assert.Single(await dbContext.Set<DocumentPassageEmbedding>().ToArrayAsync());
        Assert.Equal(DocumentPassageEmbeddingStatus.Failed, embedding.Status);
        Assert.Equal("L'indexation a été interrompue. Réessayez.", embedding.ErrorMessage);
    }

    private static DocumentEmbeddingJob CreateJob(RiposteDbContext dbContext, IAiEmbeddingTaskResolver resolver) =>
        new(dbContext, resolver, new AiExecutionRecorder(dbContext, new FixedTimeProvider(Now)), new FixedTimeProvider(Now), NullLogger<DocumentEmbeddingJob>.Instance);

    private static async Task<DocumentProcessingRun> SeedRunAsync(RiposteDbContext dbContext, string text)
    {
        var document = new StoredDocument(Guid.NewGuid(), "dce.pdf", "application/pdf", 1, new string('a', 64), Now);
        var run = new DocumentProcessingRun(document.Id, Now);
        dbContext.AddRange(document, run);
        await dbContext.SaveChangesAsync();
        run.TryStart(Now);
        run.Complete(1, 1, Now);
        dbContext.Add(new DocumentPassage(run.Id, 1, text, 1, null, null));
        await dbContext.SaveChangesAsync();
        return run;
    }

    private static RiposteDbContext CreateDbContext() => new(new DbContextOptionsBuilder<RiposteDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .Options);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class NoEmbeddingResolver : IAiEmbeddingTaskResolver
    {
        public Task<AiEmbeddingTaskClient?> ResolveAsync(CancellationToken cancellationToken) => Task.FromResult<AiEmbeddingTaskClient?>(null);
    }

    private sealed class ThrowingEmbeddingResolver : IAiEmbeddingTaskResolver
    {
        public Task<AiEmbeddingTaskClient?> ResolveAsync(CancellationToken cancellationToken) =>
            Task.FromException<AiEmbeddingTaskClient?>(new InvalidOperationException("Provider unavailable."));
    }

    private sealed class ResolvedEmbeddingResolver(IEmbeddingGenerator<string, Embedding<float>> generator, string model = "qwen") : IAiEmbeddingTaskResolver
    {
        public Task<AiEmbeddingTaskClient?> ResolveAsync(CancellationToken cancellationToken) =>
            Task.FromResult<AiEmbeddingTaskClient?>(new(generator, Guid.NewGuid(), "Embedding", model));
    }

    private sealed class FixedEmbeddingGenerator(int dimensions) : IEmbeddingGenerator<string, Embedding<float>>
    {
        public int CallCount { get; private set; }

        public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(IEnumerable<string> values, EmbeddingGenerationOptions? options = null, CancellationToken cancellationToken = default)
        {
            CallCount += values.Count();
            var vector = new float[dimensions];
            if (dimensions > 0) vector[0] = 1;
            return Task.FromResult(new GeneratedEmbeddings<Embedding<float>>(values.Select(_ => new Embedding<float>(vector))));
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }

    private sealed class CancellingEmbeddingGenerator(CancellationTokenSource cancellation) : IEmbeddingGenerator<string, Embedding<float>>
    {
        public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(IEnumerable<string> values, EmbeddingGenerationOptions? options = null, CancellationToken cancellationToken = default)
        {
            cancellation.Cancel();
            return Task.FromCanceled<GeneratedEmbeddings<Embedding<float>>>(cancellation.Token);
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }
}
