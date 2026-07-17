namespace RiposteOS.Infrastructure.Sourcing;

public sealed record TedPage(
    DateOnly PublicationDate,
    int Fetched,
    int TotalCount,
    IReadOnlyList<SourceOpportunity> Opportunities,
    int Skipped,
    IReadOnlyList<SourceImportIssue>? Issues = null);
