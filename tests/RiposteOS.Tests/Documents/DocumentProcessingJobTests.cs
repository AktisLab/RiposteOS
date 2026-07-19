using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RiposteOS.Core.Documents;
using RiposteOS.Infrastructure.Documents;
using RiposteOS.Infrastructure.Persistence;
using RiposteOS.Tests.TestSupport;

namespace RiposteOS.Tests.Documents;

public sealed class DocumentProcessingJobTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 19, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task CompletesAndPersistsOrderedPassages()
    {
        await using var dbContext = CreateDbContext();
        var storage = new TestObjectStorage();
        var document = CreateDocument();
        var run = new DocumentProcessingRun(document.Id, Now);
        dbContext.AddRange(document, run);
        await dbContext.SaveChangesAsync();
        await storage.PutAsync(document.StorageKey, new MemoryStream([1]), 1, document.ContentType, CancellationToken.None);
        var parser = new StubParser(new ParsedDocument(2,
        [
            new ParsedPassage("Premier", 1, null, null),
            new ParsedPassage("Second", 2, "Section", null),
        ]));

        await CreateJob(dbContext, storage, parser).ExecuteAsync(run.Id, CancellationToken.None);

        var storedRun = await dbContext.Set<DocumentProcessingRun>().SingleAsync();
        Assert.Equal(DocumentProcessingStatus.Completed, storedRun.Status);
        Assert.Equal(2, storedRun.PageCount);
        Assert.Equal(2, storedRun.PassageCount);
        Assert.Equal(["Premier", "Second"], await dbContext.Set<DocumentPassage>()
            .OrderBy(passage => passage.Ordinal)
            .Select(passage => passage.Text)
            .ToArrayAsync());
    }

    [Fact]
    public async Task FailureIsPersistedAndRethrown()
    {
        await using var dbContext = CreateDbContext();
        var storage = new TestObjectStorage();
        var document = CreateDocument();
        var run = new DocumentProcessingRun(document.Id, Now);
        dbContext.AddRange(document, run);
        await dbContext.SaveChangesAsync();
        await storage.PutAsync(document.StorageKey, new MemoryStream([1]), 1, document.ContentType, CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateJob(
            dbContext,
            storage,
            new StubParser(new InvalidOperationException("Docling failure")))
            .ExecuteAsync(run.Id, CancellationToken.None));

        var storedRun = await dbContext.Set<DocumentProcessingRun>().SingleAsync();
        Assert.Equal(DocumentProcessingStatus.Failed, storedRun.Status);
        Assert.Equal("L'analyse du document a échoué. Réessayez.", storedRun.ErrorMessage);
    }

    [Fact]
    public async Task CancellationIsPersistedAndRethrown()
    {
        await using var dbContext = CreateDbContext();
        var storage = new TestObjectStorage();
        var document = CreateDocument();
        var run = new DocumentProcessingRun(document.Id, Now);
        dbContext.AddRange(document, run);
        await dbContext.SaveChangesAsync();
        await storage.PutAsync(document.StorageKey, new MemoryStream([1]), 1, document.ContentType, CancellationToken.None);
        using var cancellation = new CancellationTokenSource();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CreateJob(
            dbContext,
            storage,
            new CancellingParser(cancellation))
            .ExecuteAsync(run.Id, cancellation.Token));

        var storedRun = await dbContext.Set<DocumentProcessingRun>().SingleAsync();
        Assert.Equal(DocumentProcessingStatus.Failed, storedRun.Status);
        Assert.Equal("L'analyse a été interrompue. Réessayez.", storedRun.ErrorMessage);
    }

    [Fact]
    public async Task DoesNothingWhenTheRunCannotBeStarted()
    {
        await using var dbContext = CreateDbContext();
        var document = CreateDocument();
        var run = new DocumentProcessingRun(document.Id, Now);
        run.Fail("Échec", Now);
        dbContext.AddRange(document, run);
        await dbContext.SaveChangesAsync();

        await CreateJob(dbContext, new TestObjectStorage(), new StubParser(new ParsedDocument(0, [])))
            .ExecuteAsync(run.Id, CancellationToken.None);

        Assert.Equal(DocumentProcessingStatus.Failed, run.Status);
        Assert.Empty(await dbContext.Set<DocumentPassage>().ToArrayAsync());
    }

    private static DocumentProcessingJob CreateJob(
        RiposteDbContext dbContext,
        TestObjectStorage storage,
        IDocumentParser parser)
    {
        var timeProvider = new FixedTimeProvider(Now);
        return new DocumentProcessingJob(
            dbContext,
            new DocumentProcessingStore(dbContext, timeProvider),
            storage,
            parser,
            timeProvider,
            NullLogger<DocumentProcessingJob>.Instance);
    }

    private static StoredDocument CreateDocument() => new(
        Guid.NewGuid(), "offre.pdf", "application/pdf", 1, new string('a', 64), Now);

    private static RiposteDbContext CreateDbContext() => new(new DbContextOptionsBuilder<RiposteDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .Options);

    private sealed class StubParser : IDocumentParser
    {
        private readonly Exception? _exception;
        private readonly ParsedDocument? _document;

        public StubParser(ParsedDocument document) => _document = document;

        public StubParser(Exception exception) => _exception = exception;

        public Task<ParsedDocument> ParseAsync(string fileName, string contentType, Stream content, CancellationToken cancellationToken) =>
            _exception is null ? Task.FromResult(_document!) : Task.FromException<ParsedDocument>(_exception);
    }

    private sealed class CancellingParser(CancellationTokenSource cancellation) : IDocumentParser
    {
        public Task<ParsedDocument> ParseAsync(string fileName, string contentType, Stream content, CancellationToken cancellationToken)
        {
            cancellation.Cancel();
            return Task.FromCanceled<ParsedDocument>(cancellation.Token);
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
