namespace RiposteOS.Infrastructure.Sourcing;

public sealed record BoampPage(
    DateOnly PublicationDate,
    int Fetched,
    int TotalCount,
    IReadOnlyList<SourceOpportunity> Opportunities,
    int Skipped);
