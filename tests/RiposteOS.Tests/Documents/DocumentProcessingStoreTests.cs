using Microsoft.EntityFrameworkCore;
using RiposteOS.Core.Documents;
using RiposteOS.Infrastructure.Documents;
using RiposteOS.Infrastructure.Persistence;

namespace RiposteOS.Tests.Documents;

public sealed class DocumentProcessingStoreTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 19, 10, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("application/pdf", true)]
    [InlineData("application/vnd.openxmlformats-officedocument.wordprocessingml.document", true)]
    [InlineData("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", true)]
    [InlineData("application/zip", false)]
    public void RecognizesSupportedDocumentTypes(string contentType, bool expected) =>
        Assert.Equal(expected, DocumentProcessingStore.IsSupported(contentType));

    [Fact]
    public async Task QueueCreatesRetriesAndKeepsActiveRuns()
    {
        await using var dbContext = CreateDbContext();
        var documentId = Guid.NewGuid();
        var store = CreateStore(dbContext, Now);

        var created = await store.QueueAsync(documentId, CancellationToken.None);
        await dbContext.SaveChangesAsync();
        created.Run.Fail("Échec", Now.AddMinutes(1));
        await dbContext.SaveChangesAsync();
        var retryStore = CreateStore(dbContext, Now.AddMinutes(2));
        var retried = await retryStore.QueueAsync(documentId, CancellationToken.None);
        var active = await retryStore.QueueAsync(documentId, CancellationToken.None);

        Assert.True(created.Enqueue);
        Assert.True(retried.Enqueue);
        Assert.Equal(DocumentProcessingStatus.Queued, retried.Run.Status);
        Assert.False(active.Enqueue);
        Assert.Same(retried.Run, active.Run);
    }

    [Fact]
    public async Task StartsOnlyQueuedInMemoryRuns()
    {
        await using var dbContext = CreateDbContext();
        var run = new DocumentProcessingRun(Guid.NewGuid(), Now);
        dbContext.Add(run);
        await dbContext.SaveChangesAsync();
        var store = CreateStore(dbContext, Now);

        var missing = await store.StartAsync(Guid.NewGuid(), CancellationToken.None);
        var started = await store.StartAsync(run.Id, CancellationToken.None);
        var startedAgain = await store.StartAsync(run.Id, CancellationToken.None);

        Assert.False(missing);
        Assert.True(started);
        Assert.False(startedAgain);
    }

    [Fact]
    public async Task FailsOnlyQueuedOrRunningRuns()
    {
        await using var dbContext = CreateDbContext();
        var queued = new DocumentProcessingRun(Guid.NewGuid(), Now);
        var completed = new DocumentProcessingRun(Guid.NewGuid(), Now);
        completed.TryStart(Now);
        completed.Complete(1, 1, Now);
        dbContext.AddRange(queued, completed);
        await dbContext.SaveChangesAsync();
        var store = CreateStore(dbContext, Now);

        await store.FailAsync(Guid.NewGuid(), "Ignoré", CancellationToken.None);
        await store.FailAsync(completed.Id, "Ignoré", CancellationToken.None);
        await store.FailAsync(queued.Id, "Échec", CancellationToken.None);

        Assert.Equal(DocumentProcessingStatus.Completed, completed.Status);
        Assert.Equal(DocumentProcessingStatus.Failed, queued.Status);
        Assert.Equal("Échec", queued.ErrorMessage);
    }

    private static DocumentProcessingStore CreateStore(RiposteDbContext dbContext, DateTimeOffset now) =>
        new(dbContext, new FixedTimeProvider(now));

    private static RiposteDbContext CreateDbContext() => new(new DbContextOptionsBuilder<RiposteDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .Options);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
