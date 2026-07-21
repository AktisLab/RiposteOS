using Microsoft.EntityFrameworkCore;
using RiposteOS.Core.Consultations;
using RiposteOS.Core.Documents;
using RiposteOS.Infrastructure.Ai;
using RiposteOS.Infrastructure.Ai.DocumentClassification;
using RiposteOS.Infrastructure.Consultations;
using RiposteOS.Infrastructure.Documents;
using RiposteOS.Infrastructure.Persistence;
using RiposteOS.Tests.TestSupport;

namespace RiposteOS.Tests.Consultations;

public sealed class ConsultationsFacadeEmbeddingTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 20, 17, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ListsEveryDocumentEmbeddingState()
    {
        await using var dbContext = new RiposteDbContext(new DbContextOptionsBuilder<RiposteDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        var timeProvider = new FixedTimeProvider(Now);
        var jobs = new RecordingBackgroundJobClient();
        var facade = new ConsultationsFacade(
            dbContext,
            timeProvider,
            new DocumentProcessingStore(dbContext, timeProvider),
            new DocumentClassificationStore(dbContext, timeProvider),
            jobs);
        var consultation = new Consultation("Dossier", "Acheteur", null, null, Now);
        var documents = new[]
        {
            Document("not-started.pdf"),
            Document("empty.pdf"),
            Document("queued.pdf"),
            Document("running.pdf"),
            Document("completed.pdf"),
            Document("partial.pdf"),
            Document("failed.pdf"),
        };
        dbContext.AddRange(consultation);
        dbContext.AddRange(documents);
        await dbContext.SaveChangesAsync();
        dbContext.AddRange(documents.Select(document => new ConsultationDocument(consultation.Id, document.Id, ConsultationDocumentKind.FullDce, Now)));
        var emptyRun = CompletedRun(documents[1], 0);
        var queuedRun = CompletedRun(documents[2], 1);
        var runningRun = CompletedRun(documents[3], 1);
        var completedRun = CompletedRun(documents[4], 1);
        var partialRun = CompletedRun(documents[5], 2);
        var failedRun = CompletedRun(documents[6], 1);
        dbContext.AddRange(emptyRun, queuedRun, runningRun, completedRun, partialRun, failedRun);
        await dbContext.SaveChangesAsync();

        var queuedPassage = Passage(queuedRun, 1);
        var runningPassage = Passage(runningRun, 1);
        var completedPassage = Passage(completedRun, 1);
        var partialFirst = Passage(partialRun, 1);
        var partialSecond = Passage(partialRun, 2);
        var failedPassage = Passage(failedRun, 1);
        dbContext.AddRange(queuedPassage, runningPassage, completedPassage, partialFirst, partialSecond, failedPassage);
        await dbContext.SaveChangesAsync();
        var running = Embedding(runningPassage);
        running.TryStart(Now);
        var completed = Embedding(completedPassage);
        completed.TryStart(Now);
        completed.Complete(Vector(), Now);
        var partial = Embedding(partialFirst);
        partial.TryStart(Now);
        partial.Complete(Vector(), Now);
        var failed = Embedding(failedPassage);
        failed.Fail("Relancez l'indexation.", Now);
        dbContext.AddRange(running, completed, partial, failed);
        await dbContext.SaveChangesAsync();

        var results = await facade.ListDocumentsAsync(consultation.Id, CancellationToken.None);
        var statusByDocument = results!.ToDictionary(item => item.OriginalFileName, item => item.Embedding);

        Assert.Equal("NotStarted", statusByDocument["not-started.pdf"].Status);
        Assert.Equal("Completed", statusByDocument["empty.pdf"].Status);
        Assert.Equal("Queued", statusByDocument["queued.pdf"].Status);
        Assert.Equal("Running", statusByDocument["running.pdf"].Status);
        Assert.Equal("Completed", statusByDocument["completed.pdf"].Status);
        Assert.Equal("Queued", statusByDocument["partial.pdf"].Status);
        Assert.Equal("Failed", statusByDocument["failed.pdf"].Status);
        Assert.Equal(1, statusByDocument["partial.pdf"].IndexedPassageCount);
        Assert.Equal("Relancez l'indexation.", statusByDocument["failed.pdf"].ErrorMessage);
        Assert.Empty((await facade.ListDocumentPassagesAsync(consultation.Id, documents[0].Id, CancellationToken.None))!);
        Assert.Null(await facade.ListDocumentPassagesAsync(consultation.Id, Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task RetriesOnlyACompletedDocumentWhoseEmbeddingFailed()
    {
        await using var dbContext = new RiposteDbContext(new DbContextOptionsBuilder<RiposteDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        var timeProvider = new FixedTimeProvider(Now);
        var jobs = new RecordingBackgroundJobClient();
        var facade = new ConsultationsFacade(
            dbContext,
            timeProvider,
            new DocumentProcessingStore(dbContext, timeProvider),
            new DocumentClassificationStore(dbContext, timeProvider),
            jobs);
        var consultation = new Consultation("Dossier", "Acheteur", null, null, Now);
        var notProcessed = Document("not-processed.pdf");
        var queued = Document("queued.pdf");
        var failed = Document("failed.pdf");
        dbContext.AddRange(consultation, notProcessed, queued, failed);
        await dbContext.SaveChangesAsync();
        dbContext.AddRange(
            new ConsultationDocument(consultation.Id, notProcessed.Id, ConsultationDocumentKind.FullDce, Now),
            new ConsultationDocument(consultation.Id, queued.Id, ConsultationDocumentKind.FullDce, Now),
            new ConsultationDocument(consultation.Id, failed.Id, ConsultationDocumentKind.FullDce, Now));
        var queuedRun = CompletedRun(queued, 1);
        var failedRun = CompletedRun(failed, 1);
        dbContext.AddRange(queuedRun, failedRun);
        await dbContext.SaveChangesAsync();
        var queuedPassage = Passage(queuedRun, 1);
        var failedPassage = Passage(failedRun, 1);
        dbContext.AddRange(queuedPassage, failedPassage);
        await dbContext.SaveChangesAsync();
        var failedEmbedding = Embedding(failedPassage);
        failedEmbedding.Fail("Erreur", Now);
        dbContext.Add(failedEmbedding);
        await dbContext.SaveChangesAsync();

        Assert.Null(await facade.RetryDocumentEmbeddingAsync(consultation.Id, Guid.NewGuid(), CancellationToken.None));
        Assert.Equal("NotStarted", (await facade.RetryDocumentEmbeddingAsync(consultation.Id, notProcessed.Id, CancellationToken.None))!.Embedding.Status);
        Assert.Null(jobs.CreatedJob);

        Assert.Equal("Queued", (await facade.RetryDocumentEmbeddingAsync(consultation.Id, queued.Id, CancellationToken.None))!.Embedding.Status);
        Assert.NotNull(jobs.CreatedJob);
        jobs.Reset();

        var retried = await facade.RetryDocumentEmbeddingAsync(consultation.Id, failed.Id, CancellationToken.None);

        Assert.Equal("Failed", retried!.Embedding.Status);
        Assert.NotNull(jobs.CreatedJob);
    }

    private static StoredDocument Document(string fileName) =>
        new(Guid.NewGuid(), fileName, "application/pdf", 1, new string('a', 64), Now);

    private static DocumentProcessingRun CompletedRun(StoredDocument document, int passageCount)
    {
        var run = new DocumentProcessingRun(document.Id, Now);
        run.TryStart(Now);
        run.Complete(passageCount, passageCount, Now);
        return run;
    }

    private static DocumentPassage Passage(DocumentProcessingRun run, int ordinal) =>
        new(run.Id, ordinal, $"Passage {ordinal}", ordinal, null, null);

    private static DocumentPassageEmbedding Embedding(DocumentPassage passage) =>
        new(passage.Id, new string('b', 64), "Embedding", "qwen", Now);

    private static float[] Vector()
    {
        var vector = new float[DocumentPassageEmbedding.ExpectedDimension];
        vector[0] = 1;
        return vector;
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
