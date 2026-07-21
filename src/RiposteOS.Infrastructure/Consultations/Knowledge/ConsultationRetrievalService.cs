using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pgvector;
using RiposteOS.Core.Consultations;
using RiposteOS.Core.Documents;
using RiposteOS.Infrastructure.Ai;
using RiposteOS.Infrastructure.Ai.Tasks;
using RiposteOS.Infrastructure.Persistence;

namespace RiposteOS.Infrastructure.Consultations.Knowledge;

public sealed class ConsultationRetrievalService(RiposteDbContext dbContext, IAiEmbeddingTaskResolver embeddingResolver)
{
    private const int CandidateLimit = 30;
    private const int EvidenceLimit = 8;

    public async Task<ConsultationRetrievalResult> RetrieveAsync(Guid consultationId, string question, CancellationToken cancellationToken)
    {
        if (consultationId == Guid.Empty) throw new ArgumentException("A consultation is required.", nameof(consultationId));
        if (string.IsNullOrWhiteSpace(question)) throw new ArgumentException("A question is required.", nameof(question));
        var client = await embeddingResolver.ResolveAsync(cancellationToken);
        if (client is null) return ConsultationRetrievalResult.NotConfigured;
        var generated = await client.Generator.GenerateAsync([question.Trim()], cancellationToken: cancellationToken);
        var questionEmbedding = generated[0].Vector.ToArray();
        if (questionEmbedding.Length != DocumentPassageEmbedding.ExpectedDimension) throw new InvalidOperationException("Le modèle d'embedding configuré doit produire 1024 dimensions.");

        var semantic = dbContext.Database.IsRelational()
            ? await SemanticSearchAsync(consultationId, questionEmbedding, client.ProviderName, client.Model, cancellationToken)
            : await SemanticSearchInMemoryAsync(consultationId, questionEmbedding, client.ProviderName, client.Model, cancellationToken);
        var lexical = dbContext.Database.IsRelational()
            ? await LexicalSearchAsync(consultationId, question, client.ProviderName, client.Model, cancellationToken)
            : await LexicalSearchInMemoryAsync(consultationId, question, client.ProviderName, client.Model, cancellationToken);
        var ranks = Fuse(semantic, lexical);
        if (ranks.Count == 0) return new ConsultationRetrievalResult([], true);

        var passages = await (from link in dbContext.Set<ConsultationDocument>().AsNoTracking()
                              join document in dbContext.Set<StoredDocument>().AsNoTracking() on link.StoredDocumentId equals document.Id
                              join run in dbContext.Set<DocumentProcessingRun>().AsNoTracking() on document.Id equals run.StoredDocumentId
                              join passage in dbContext.Set<DocumentPassage>().AsNoTracking() on run.Id equals passage.DocumentProcessingRunId
                              where link.ConsultationId == consultationId && ranks.Keys.Contains(passage.Id)
                              select new ConsultationEvidence(
                                  passage.Id,
                                  ranks[passage.Id],
                                  document.Id,
                                  document.OriginalFileName,
                                  passage.PageNumber,
                                  passage.SectionTitle,
                                  passage.Ordinal,
                                  passage.Text)).ToArrayAsync(cancellationToken);
        return new ConsultationRetrievalResult(passages.OrderByDescending(item => item.Score).ThenBy(item => item.Ordinal).Take(EvidenceLimit).ToArray(), true);
    }

    private async Task<Dictionary<Guid, double>> SemanticSearchAsync(Guid consultationId, float[] embedding, string providerName, string model, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT embedding."DocumentPassageId", 1 - (embedding."Embedding" <=> @embedding) AS score
            FROM ai.document_passage_embeddings AS embedding
            JOIN documents.document_passages AS passage ON passage."Id" = embedding."DocumentPassageId"
            JOIN documents.document_processing_runs AS run ON run."Id" = passage."DocumentProcessingRunId"
            JOIN consultations.consultation_documents AS link ON link."StoredDocumentId" = run."StoredDocumentId"
            WHERE link."ConsultationId" = @consultationId
              AND embedding."Status" = 'Completed'
              AND embedding."ProviderName" = @providerName
              AND embedding."Model" = @model
              AND embedding."Embedding" IS NOT NULL
            ORDER BY embedding."Embedding" <=> @embedding
            LIMIT 30;
            """;
        return await QueryRanksAsync(sql, consultationId, providerName, model, new Vector(embedding), cancellationToken);
    }

    private async Task<Dictionary<Guid, double>> LexicalSearchAsync(Guid consultationId, string question, string providerName, string model, CancellationToken cancellationToken)
    {
        const string sql = """
            WITH search_query AS (
                SELECT to_tsquery('french', array_to_string(tsvector_to_array(to_tsvector('french', @question)), ' | ')) AS value
            )
            SELECT passage."Id", ts_rank_cd(
                setweight(to_tsvector('french', coalesce(passage."SectionTitle", '')), 'A') || setweight(to_tsvector('french', passage."Text"), 'B'),
                search_query.value) AS score
            FROM documents.document_passages AS passage
            JOIN documents.document_processing_runs AS run ON run."Id" = passage."DocumentProcessingRunId"
            JOIN consultations.consultation_documents AS link ON link."StoredDocumentId" = run."StoredDocumentId"
            JOIN ai.document_passage_embeddings AS embedding ON embedding."DocumentPassageId" = passage."Id"
            CROSS JOIN search_query
            WHERE link."ConsultationId" = @consultationId
              AND embedding."Status" = 'Completed'
              AND embedding."ProviderName" = @providerName
              AND embedding."Model" = @model
              AND (setweight(to_tsvector('french', coalesce(passage."SectionTitle", '')), 'A') || setweight(to_tsvector('french', passage."Text"), 'B')) @@ search_query.value
            ORDER BY score DESC
            LIMIT 30;
            """;
        return await QueryRanksAsync(sql, consultationId, providerName, model, question, cancellationToken);
    }

    private async Task<Dictionary<Guid, double>> QueryRanksAsync(string sql, Guid consultationId, string providerName, string model, object value, CancellationToken cancellationToken)
    {
        var connection = (NpgsqlConnection)dbContext.Database.GetDbConnection();
        var mustClose = connection.State != System.Data.ConnectionState.Open;
        if (mustClose) await connection.OpenAsync(cancellationToken);
        try
        {
            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("consultationId", consultationId);
            command.Parameters.AddWithValue("providerName", providerName);
            command.Parameters.AddWithValue("model", model);
            command.Parameters.AddWithValue(value is Vector vector ? "embedding" : "question", value);
            var result = new Dictionary<Guid, double>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken)) result.Add(reader.GetGuid(0), reader.GetDouble(1));
            return result;
        }
        finally
        {
            if (mustClose) await connection.CloseAsync();
        }
    }

    private async Task<Dictionary<Guid, double>> SemanticSearchInMemoryAsync(Guid consultationId, float[] embedding, string providerName, string model, CancellationToken cancellationToken)
    {
        var candidates = await CandidateEmbeddingsAsync(consultationId, providerName, model, cancellationToken);
        return candidates.OrderByDescending(item => Cosine(item.Embedding!, embedding)).Take(CandidateLimit).ToDictionary(item => item.DocumentPassageId, item => Cosine(item.Embedding!, embedding));
    }

    private async Task<Dictionary<Guid, double>> LexicalSearchInMemoryAsync(Guid consultationId, string question, string providerName, string model, CancellationToken cancellationToken)
    {
        var words = question.Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var textByPassage = await (from link in dbContext.Set<ConsultationDocument>().AsNoTracking()
                                   join run in dbContext.Set<DocumentProcessingRun>().AsNoTracking() on link.StoredDocumentId equals run.StoredDocumentId
                                   join passage in dbContext.Set<DocumentPassage>().AsNoTracking() on run.Id equals passage.DocumentProcessingRunId
                                   join embedding in dbContext.Set<DocumentPassageEmbedding>().AsNoTracking() on passage.Id equals embedding.DocumentPassageId
                                   where link.ConsultationId == consultationId && embedding.Status == DocumentPassageEmbeddingStatus.Completed && embedding.ProviderName == providerName && embedding.Model == model
                                   select new { passage.Id, passage.Text }).ToArrayAsync(cancellationToken);
        return textByPassage.Select(item => new { item.Id, Score = words.Count(word => item.Text.Contains(word, StringComparison.OrdinalIgnoreCase)) }).Where(item => item.Score > 0).OrderByDescending(item => item.Score).Take(CandidateLimit).ToDictionary(item => item.Id, item => (double)item.Score);
    }

    private Task<DocumentPassageEmbedding[]> CandidateEmbeddingsAsync(Guid consultationId, string providerName, string model, CancellationToken cancellationToken) =>
        (from link in dbContext.Set<ConsultationDocument>().AsNoTracking()
         join run in dbContext.Set<DocumentProcessingRun>().AsNoTracking() on link.StoredDocumentId equals run.StoredDocumentId
         join passage in dbContext.Set<DocumentPassage>().AsNoTracking() on run.Id equals passage.DocumentProcessingRunId
         join embedding in dbContext.Set<DocumentPassageEmbedding>().AsNoTracking() on passage.Id equals embedding.DocumentPassageId
         where link.ConsultationId == consultationId && embedding.Status == DocumentPassageEmbeddingStatus.Completed && embedding.ProviderName == providerName && embedding.Model == model
         select embedding).ToArrayAsync(cancellationToken);

    private static Dictionary<Guid, double> Fuse(Dictionary<Guid, double> semantic, Dictionary<Guid, double> lexical)
    {
        var scores = new Dictionary<Guid, double>();
        foreach (var ranked in new[] { semantic, lexical })
        {
            foreach (var item in ranked.OrderByDescending(item => item.Value).Select((item, index) => new { item.Key, Rank = index + 1 })) scores[item.Key] = scores.GetValueOrDefault(item.Key) + 1d / (60 + item.Rank);
        }
        return scores;
    }

    private static double Cosine(float[] left, float[] right)
    {
        var dot = 0d; var leftNorm = 0d; var rightNorm = 0d;
        for (var index = 0; index < left.Length; index++) { dot += left[index] * right[index]; leftNorm += left[index] * left[index]; rightNorm += right[index] * right[index]; }
        return leftNorm == 0 || rightNorm == 0 ? 0 : dot / Math.Sqrt(leftNorm * rightNorm);
    }
}
