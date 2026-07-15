namespace RiposteOS.Infrastructure.Sourcing;

public sealed record SourcingPage(
    DateOnly PublicationDate,
    int Fetched,
    IReadOnlyList<SourceOpportunity> Opportunities,
    int Skipped);
