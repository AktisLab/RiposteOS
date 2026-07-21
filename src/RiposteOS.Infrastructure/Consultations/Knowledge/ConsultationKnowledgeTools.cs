using Microsoft.Extensions.AI;

namespace RiposteOS.Infrastructure.Consultations.Knowledge;

public sealed class ConsultationKnowledgeTools(
    ConsultationKnowledgeFacade knowledge,
    Guid consultationId,
    PassageReferenceSet references,
    string userQuestion,
    Func<string, CancellationToken, ValueTask> reportActivity)
{
    private const int MaximumSearchQueries = 4;
    private const int MaximumSearchResults = 16;
    private const int MaximumExpandedSections = 2;
    private const int MaximumExpandedPassagesPerSection = 8;
    private const int MaximumToolPassages = 32;
    private const string DeliverablesDiscoveryExpansion = "rapport étonnement comptes rendus ateliers";
    private const string DeliverablesDesignExpansion = "wireframes maquettes graphiques prototypes";
    private const string DeliverablesTechnicalExpansion = "code source documentation technique intégration déploiement";

    public bool SearchAttempted { get; private set; }
    public bool IsConfigured { get; private set; }
    public string[] SearchQueries { get; private set; } = [];

    public AIFunction[] Create() =>
    [
        AIFunctionFactory.Create((Func<string[], CancellationToken, Task<ConsultationToolSearchResult>>)SearchPassagesAsync, "search_passages", "Recherche les passages du DCE nécessaires pour répondre. Fournis 2 à 4 requêtes complémentaires couvrant les différents volets de la question. Cet outil doit être appelé avant toute réponse."),
        AIFunctionFactory.Create((Func<string, CancellationToken, Task<ConsultationToolContextResult>>)GetPassageContextAsync, "get_passage_context", "Lit le contexte immédiat d'une preuve P1, P2, etc. déjà retournée par la recherche."),
        AIFunctionFactory.Create((Func<CancellationToken, Task<ConsultationKnowledgeDocument[]>>)ListDocumentsAsync, "list_documents", "Liste les documents du DCE disponibles et leur état d'indexation."),
        AIFunctionFactory.Create((Func<Guid, CancellationToken, Task<ConsultationToolOutlineResult>>)GetDocumentOutlineAsync, "get_document_outline", "Liste les sections indexées d'un document déjà identifié par son identifiant. N'utilise jamais un identifiant inventé."),
        AIFunctionFactory.Create((Func<Guid, string, CancellationToken, Task<ConsultationToolSectionResult>>)GetDocumentSectionAsync, "get_document_section", "Lit les passages d'une section retournée par get_document_outline. Utilise exactement le documentId et le sectionTitle fournis par les outils."),
    ];

    public async Task<ConsultationToolSearchResult> SearchPassagesAsync(
        string[] queries,
        CancellationToken cancellationToken)
    {
        await reportActivity("Recherche de passages complémentaires dans le DCE…", cancellationToken);
        SearchAttempted = true;
        var normalized = (queries ?? [])
            .Take(MaximumSearchQueries)
            .Append(userQuestion)
            .Concat(ExpandUserQuestion(userQuestion))
            .Where(query => !string.IsNullOrWhiteSpace(query))
            .Select(query => query.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        SearchQueries = normalized;
        if (normalized.Length == 0)
        {
            IsConfigured = true;
            return new ConsultationToolSearchResult([], true);
        }

        var results = new List<ConsultationRetrievalResult>(normalized.Length);
        foreach (var query in normalized)
        {
            results.Add(await knowledge.SearchAsync(consultationId, query, cancellationToken));
        }

        IsConfigured = results.All(result => result.IsConfigured);
        var fused = results
            .SelectMany(result => result.Evidence.Select((evidence, index) => new { Evidence = evidence, Rank = index + 1 }))
            .GroupBy(item => item.Evidence.PassageId)
            .Select(group => new
            {
                Evidence = group.OrderByDescending(item => item.Evidence.Score).First().Evidence,
                FusionScore = group.Max(item => 1d / (60 + item.Rank)),
            })
            .OrderByDescending(item => item.FusionScore)
            .ThenByDescending(item => item.Evidence.Score)
            .ThenBy(item => item.Evidence.Ordinal)
            .Take(MaximumSearchResults)
            .Select(item => item.Evidence with { Score = item.FusionScore })
            .ToArray();
        var expanded = new List<ConsultationEvidence>();
        foreach (var section in fused
                     .Where(item => !string.IsNullOrWhiteSpace(item.SectionTitle))
                     .DistinctBy(item => (item.DocumentId, item.SectionTitle))
                     .Take(MaximumExpandedSections))
        {
            expanded.AddRange((await knowledge.GetDocumentSectionAsync(
                    consultationId,
                    section.DocumentId,
                    section.SectionTitle!,
                    cancellationToken))
                .Take(MaximumExpandedPassagesPerSection));
        }

        var passages = fused
            .Concat(expanded)
            .DistinctBy(item => item.PassageId)
            .Take(MaximumToolPassages);
        return new ConsultationToolSearchResult(references.Register(passages), IsConfigured);
    }

    private static IEnumerable<string> ExpandUserQuestion(string question)
    {
        if (question.Contains("livr", StringComparison.OrdinalIgnoreCase)
            || question.Contains("rendu", StringComparison.OrdinalIgnoreCase))
        {
            yield return DeliverablesDiscoveryExpansion;
            yield return DeliverablesDesignExpansion;
            yield return DeliverablesTechnicalExpansion;
        }
    }

    public async Task<ConsultationToolContextResult> GetPassageContextAsync(
        string reference,
        CancellationToken cancellationToken)
    {
        await reportActivity("Lecture du contexte d’un passage du DCE…", cancellationToken);
        if (string.IsNullOrWhiteSpace(reference) || !references.TryGetPassageId(reference, out var passageId))
        {
            return new ConsultationToolContextResult(reference, []);
        }

        var passages = await knowledge.GetPassageContextAsync(consultationId, passageId, cancellationToken);
        return new ConsultationToolContextResult(reference, references.Register(passages));
    }

    public async Task<ConsultationKnowledgeDocument[]> ListDocumentsAsync(CancellationToken cancellationToken)
    {
        await reportActivity("Vérification des documents disponibles…", cancellationToken);
        return await knowledge.ListDocumentsAsync(consultationId, cancellationToken);
    }

    public async Task<ConsultationToolOutlineResult> GetDocumentOutlineAsync(
        Guid documentId,
        CancellationToken cancellationToken)
    {
        await reportActivity("Lecture de la structure d’un document…", cancellationToken);
        if (documentId == Guid.Empty) return new ConsultationToolOutlineResult(documentId, []);
        var sections = await knowledge.GetDocumentOutlineAsync(consultationId, documentId, cancellationToken);
        return new ConsultationToolOutlineResult(documentId, sections);
    }

    public async Task<ConsultationToolSectionResult> GetDocumentSectionAsync(
        Guid documentId,
        string sectionTitle,
        CancellationToken cancellationToken)
    {
        await reportActivity("Lecture d’une section du DCE…", cancellationToken);
        if (documentId == Guid.Empty || string.IsNullOrWhiteSpace(sectionTitle))
        {
            return new ConsultationToolSectionResult(documentId, sectionTitle, []);
        }

        var passages = await knowledge.GetDocumentSectionAsync(consultationId, documentId, sectionTitle, cancellationToken);
        return new ConsultationToolSectionResult(documentId, sectionTitle, references.Register(passages));
    }
}
