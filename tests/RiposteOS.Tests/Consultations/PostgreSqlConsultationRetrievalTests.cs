using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using RiposteOS.Core.Ai;
using RiposteOS.Core.Consultations;
using RiposteOS.Core.Documents;
using RiposteOS.Infrastructure.Ai;
using RiposteOS.Infrastructure.Ai.Providers;
using RiposteOS.Infrastructure.Ai.Tasks;
using RiposteOS.Infrastructure.Consultations.Knowledge;
using RiposteOS.Infrastructure.Consultations;
using RiposteOS.Infrastructure.Persistence;
using RiposteOS.Tests.TestSupport;

namespace RiposteOS.Tests.Consultations;

[Collection(PostgreSqlTestGroup.Name)]
public sealed class PostgreSqlConsultationRetrievalTests(PostgreSqlFixture fixture)
{
    private static readonly DateTimeOffset Now = new(2026, 7, 20, 16, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task RetrievalUsesPgvectorAndFrenchLexicalSearchWithoutLeavingTheConsultation()
    {
        var connectionString = await fixture.CreateDatabaseAsync();
        await using var dbContext = PostgreSqlFixture.CreateContext(connectionString);
        var expected = await AddIndexedPassageAsync(dbContext, "Délai de remise des offres fixé au 12 septembre.");
        var otherConsultation = new Consultation("Autre dossier", "Acheteur", null, null, Now);
        var otherDocument = CreateDocument("autre.pdf");
        dbContext.AddRange(otherConsultation, otherDocument);
        await dbContext.SaveChangesAsync();
        dbContext.Add(new ConsultationDocument(otherConsultation.Id, otherDocument.Id, ConsultationDocumentKind.FullDce, Now));
        var otherRun = new DocumentProcessingRun(otherDocument.Id, Now);
        dbContext.Add(otherRun);
        await dbContext.SaveChangesAsync();
        otherRun.TryStart(Now);
        otherRun.Complete(1, 1, Now);
        var otherPassage = new DocumentPassage(otherRun.Id, 1, "Délai de remise concurrentiel qui ne doit pas être cité.", 1, null, null);
        dbContext.Add(otherPassage);
        await dbContext.SaveChangesAsync();
        var otherEmbedding = new DocumentPassageEmbedding(otherPassage.Id, new string('b', 64), "Embedding", "qwen", Now);
        otherEmbedding.TryStart(Now);
        otherEmbedding.Complete(Vector(), Now);
        dbContext.Add(otherEmbedding);
        await dbContext.SaveChangesAsync();

        var service = new ConsultationRetrievalService(dbContext, new FixedEmbeddingResolver());
        var result = await service.RetrieveAsync(expected.ConsultationId, "Quel est le délai de remise des offres ?", CancellationToken.None);

        var evidence = Assert.Single(result.Evidence);
        Assert.True(result.IsConfigured);
        Assert.Equal(expected.PassageId, evidence.PassageId);
        Assert.Equal(expected.DocumentId, evidence.DocumentId);
        Assert.Contains("12 septembre", evidence.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TaskResolversSupportTextStoredProviderCapabilitiesOnPostgreSql()
    {
        var connectionString = await fixture.CreateDatabaseAsync();
        await using var dbContext = PostgreSqlFixture.CreateContext(connectionString);
        var embedding = new AiProvider(Guid.NewGuid(), "Embedding", AiProviderProtocol.OpenAiCompatible, "https://embedding.example.test/v1", "qwen", null, true, Now, Now, AiProviderCapabilities.Embedding);
        var chat = new AiProvider(Guid.NewGuid(), "Chat", AiProviderProtocol.OpenAiCompatible, "https://chat.example.test/v1", "gpt", null, true, Now, Now, AiProviderCapabilities.Chat | AiProviderCapabilities.ToolCalling);
        dbContext.AddRange(
            embedding,
            chat,
            new AiTaskAssignment(AiTask.DocumentEmbedding, embedding.Id, Now),
            new AiTaskAssignment(AiTask.ConsultationChat, chat.Id, Now));
        await dbContext.SaveChangesAsync();

        var embeddingResult = await new AiEmbeddingTaskResolver(dbContext, new FixedEmbeddingFactory()).ResolveAsync(CancellationToken.None);
        var chatResult = await new AiTaskClientResolver(dbContext, new FixedChatFactory()).ResolveAsync(AiTask.ConsultationChat, CancellationToken.None);

        Assert.Equal(embedding.Id, embeddingResult!.ProviderId);
        Assert.Equal(chat.Id, chatResult!.ProviderId);
    }

    private static async Task<(Guid ConsultationId, Guid DocumentId, Guid PassageId)> AddIndexedPassageAsync(RiposteDbContext dbContext, string text)
    {
        var consultation = new Consultation("Dossier cible", "Acheteur", null, null, Now);
        var document = CreateDocument("dce.pdf");
        dbContext.AddRange(consultation, document);
        await dbContext.SaveChangesAsync();
        dbContext.Add(new ConsultationDocument(consultation.Id, document.Id, ConsultationDocumentKind.FullDce, Now));
        var run = new DocumentProcessingRun(document.Id, Now);
        dbContext.Add(run);
        await dbContext.SaveChangesAsync();
        run.TryStart(Now);
        run.Complete(1, 1, Now);
        var passage = new DocumentPassage(run.Id, 1, text, 1, "Calendrier", "page 1");
        dbContext.Add(passage);
        await dbContext.SaveChangesAsync();
        var embedding = new DocumentPassageEmbedding(passage.Id, new string('a', 64), "Embedding", "qwen", Now);
        embedding.TryStart(Now);
        embedding.Complete(Vector(), Now);
        dbContext.Add(embedding);
        await dbContext.SaveChangesAsync();
        return (consultation.Id, document.Id, passage.Id);
    }

    private static StoredDocument CreateDocument(string fileName) => new(Guid.NewGuid(), fileName, "application/pdf", 1, new string('a', 64), Now);

    private static float[] Vector()
    {
        var values = new float[DocumentPassageEmbedding.ExpectedDimension];
        values[0] = 1;
        return values;
    }

    private sealed class FixedEmbeddingResolver : IAiEmbeddingTaskResolver
    {
        public Task<AiEmbeddingTaskClient?> ResolveAsync(CancellationToken cancellationToken) =>
            Task.FromResult<AiEmbeddingTaskClient?>(new(new FixedEmbeddingGenerator(), Guid.NewGuid(), "Embedding", "qwen"));
    }

    private sealed class FixedEmbeddingFactory : IAiEmbeddingGeneratorFactory
    {
        public IEmbeddingGenerator<string, Embedding<float>> Create(AiProvider provider) => new FixedEmbeddingGenerator();
    }

    private sealed class FixedChatFactory : IAiChatClientFactory
    {
        public IChatClient Create(AiProvider provider) => null!;
    }

    private sealed class FixedEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
    {
        public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(IEnumerable<string> values, EmbeddingGenerationOptions? options = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(new GeneratedEmbeddings<Embedding<float>>(values.Select(_ => new Embedding<float>(Vector()))));

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }
}
