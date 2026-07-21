using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using RiposteOS.Core.Consultations;
using RiposteOS.Core.Documents;
using RiposteOS.Infrastructure.Ai;
using RiposteOS.Infrastructure.Ai.Tasks;
using RiposteOS.Infrastructure.Consultations;
using RiposteOS.Infrastructure.Consultations.Knowledge;
using RiposteOS.Infrastructure.Persistence;

namespace RiposteOS.Tests.Consultations;

public sealed class ConsultationKnowledgeToolsTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 20, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public void RegistryKeepsStableOpaqueReferencesAndRejectsUnknownOnes()
    {
        var registry = new PassageReferenceSet();
        var evidence = new ConsultationEvidence(Guid.NewGuid(), 1, Guid.NewGuid(), "dce.pdf", 4, "Maintenance", 2, "Preuve");

        Assert.Equal("P1", registry.Register(evidence).Reference);
        Assert.Equal("P1", registry.Register(evidence).Reference);
        Assert.True(registry.TryGetPassageId("[P1]", out var passageId));
        Assert.Equal(evidence.PassageId, passageId);
        Assert.False(registry.TryGetPassageId("P99", out _));
        Assert.True(registry.TryResolveCitations("Réponse [P1] puis (P1, P1).", out var resolved));
        Assert.Single(resolved);
        Assert.False(registry.TryResolveCitations("Réponse [P2].", out resolved));
        Assert.Empty(resolved);
        Assert.Equal("Réponse.", PassageReferenceSet.RemoveCitationMarkers("Réponse (P1, P2)."));
        Assert.Equal("Réponse.", PassageReferenceSet.RemoveCitationMarkers("Réponse [P4‑P7]."));
    }

    [Fact]
    public async Task ToolsOnlyExposeRegisteredContextFromTheCurrentConsultation()
    {
        await using var dbContext = CreateDbContext();
        var (consultation, document, passages) = await SeedAsync(dbContext);
        var registry = new PassageReferenceSet();
        var activities = new List<string>();
        var retrieval = new ConsultationRetrievalService(dbContext, new FixedEmbeddingResolver());
        var tools = new ConsultationKnowledgeTools(
            new ConsultationKnowledgeFacade(dbContext, retrieval),
            consultation.Id,
            registry,
            "Quels livrables sont attendus ?",
            (activity, _) =>
            {
                activities.Add(activity);
                return ValueTask.CompletedTask;
            });

        Assert.NotEmpty((await tools.SearchPassagesAsync(["maintenance", "anomalies incidents"], CancellationToken.None)).Passages);
        Assert.True(tools.SearchAttempted);
        Assert.Contains(tools.SearchQueries, query => query.Contains("wireframes", StringComparison.Ordinal));
        Assert.Empty((await tools.GetPassageContextAsync("P99", CancellationToken.None)).Passages);
        var targetReference = registry.Register(new ConsultationEvidence(passages[1].Id, 1, document.Id, document.OriginalFileName, 2, "Maintenance", 2, passages[1].Text)).Reference;

        var context = await tools.GetPassageContextAsync($"[{targetReference}]", CancellationToken.None);
        var listed = await tools.ListDocumentsAsync(CancellationToken.None);
        var emptyOutline = await tools.GetDocumentOutlineAsync(Guid.Empty, CancellationToken.None);
        var outline = await tools.GetDocumentOutlineAsync(document.Id, CancellationToken.None);
        var emptySection = await tools.GetDocumentSectionAsync(Guid.Empty, "", CancellationToken.None);
        var section = await tools.GetDocumentSectionAsync(document.Id, "Maintenance", CancellationToken.None);

        Assert.Equal(3, context.Passages.Length);
        Assert.Equal(passages.Select(item => item.Id), context.Passages.Select(item => registry.TryGetPassageId(item.Reference, out var id) ? id : Guid.Empty));
        var listedDocument = Assert.Single(listed);
        Assert.Equal(document.Id, listedDocument.DocumentId);
        Assert.Equal(document.OriginalFileName, listedDocument.DocumentName);
        Assert.Equal(nameof(ConsultationDocumentKind.FullDce), listedDocument.Kind);
        Assert.True(listedDocument.IsIndexed);
        Assert.Empty(emptyOutline.Sections);
        Assert.Equal(3, outline.Sections.Length);
        Assert.Empty(emptySection.Passages);
        Assert.Equal(2, section.Passages.Length);
        Assert.Equal(8, activities.Count);
    }

    [Fact]
    public async Task KnowledgeFacadeNeverReturnsPassagesFromAnotherConsultation()
    {
        await using var dbContext = CreateDbContext();
        var first = await SeedAsync(dbContext);
        var second = await SeedAsync(dbContext);
        var retrieval = new ConsultationRetrievalService(dbContext, new FixedEmbeddingResolver());
        var knowledge = new ConsultationKnowledgeFacade(dbContext, retrieval);

        var context = await knowledge.GetPassageContextAsync(first.Consultation.Id, second.Passages[0].Id, CancellationToken.None);
        var outline = await knowledge.GetDocumentOutlineAsync(first.Consultation.Id, second.Document.Id, CancellationToken.None);
        var section = await knowledge.GetDocumentSectionAsync(first.Consultation.Id, second.Document.Id, "Maintenance", CancellationToken.None);
        var passages = await knowledge.GetPassagesAsync(first.Consultation.Id, [first.Passages[0].Id, second.Passages[0].Id], CancellationToken.None);

        Assert.Empty(context);
        Assert.Empty(outline);
        Assert.Empty(section);
        Assert.Equal([first.Passages[0].Id], passages.Keys);
    }

    [Fact]
    public async Task BlankInitialSearchReturnsNoEvidenceWithoutCallingTheEmbeddingProvider()
    {
        await using var dbContext = CreateDbContext();
        var consultation = new Consultation("Dossier", "Acheteur", null, null, Now);
        dbContext.Add(consultation);
        await dbContext.SaveChangesAsync();
        var references = new PassageReferenceSet();
        var retrieval = new ConsultationRetrievalService(dbContext, new FixedEmbeddingResolver());
        var tools = new ConsultationKnowledgeTools(
            new ConsultationKnowledgeFacade(dbContext, retrieval),
            consultation.Id,
            references,
            " ",
            (_, _) => ValueTask.CompletedTask);

        var result = await tools.SearchPassagesAsync([" "], CancellationToken.None);

        Assert.True(tools.SearchAttempted);
        Assert.True(tools.IsConfigured);
        Assert.True(result.IsConfigured);
        Assert.Empty(result.Passages);
        Assert.Equal(0, references.Count);
    }

    private static RiposteDbContext CreateDbContext() => new(new DbContextOptionsBuilder<RiposteDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static async Task<(Consultation Consultation, StoredDocument Document, DocumentPassage[] Passages)> SeedAsync(RiposteDbContext dbContext)
    {
        var consultation = new Consultation("Dossier", "Acheteur", null, null, Now);
        var document = new StoredDocument(Guid.NewGuid(), "dce.pdf", "application/pdf", 1, new string('a', 64), Now);
        dbContext.AddRange(consultation, document);
        await dbContext.SaveChangesAsync();
        var processingRun = new DocumentProcessingRun(document.Id, Now);
        dbContext.AddRange(new ConsultationDocument(consultation.Id, document.Id, ConsultationDocumentKind.FullDce, Now), processingRun);
        await dbContext.SaveChangesAsync();
        processingRun.TryStart(Now);
        processingRun.Complete(3, 3, Now);
        var passages = new[]
        {
            new DocumentPassage(processingRun.Id, 1, "Maintenance corrective", 1, "Maintenance", null),
            new DocumentPassage(processingRun.Id, 2, "Maintenance préventive", 2, "Maintenance", null),
            new DocumentPassage(processingRun.Id, 3, "Planning", 3, "Planning", null),
        };
        dbContext.AddRange(passages);
        await dbContext.SaveChangesAsync();
        foreach (var passage in passages)
        {
            var embedding = new DocumentPassageEmbedding(passage.Id, new string('b', 64), "Embedding", "qwen", Now);
            embedding.TryStart(Now);
            embedding.Complete(Vector(), Now);
            dbContext.Add(embedding);
        }

        await dbContext.SaveChangesAsync();
        return (consultation, document, passages);
    }

    private static float[] Vector()
    {
        var value = new float[DocumentPassageEmbedding.ExpectedDimension];
        value[0] = 1;
        return value;
    }

    private sealed class FixedEmbeddingResolver : IAiEmbeddingTaskResolver
    {
        public Task<AiEmbeddingTaskClient?> ResolveAsync(CancellationToken cancellationToken) => Task.FromResult<AiEmbeddingTaskClient?>(new(new FixedEmbeddingGenerator(), Guid.NewGuid(), "Embedding", "qwen"));
    }

    private sealed class FixedEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
    {
        public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(IEnumerable<string> values, EmbeddingGenerationOptions? options = null, CancellationToken cancellationToken = default) => Task.FromResult(new GeneratedEmbeddings<Embedding<float>>(values.Select(_ => new Embedding<float>(Vector()))));
        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }
}
