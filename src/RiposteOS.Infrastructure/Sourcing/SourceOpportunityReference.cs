using RiposteOS.Core.Sourcing;

namespace RiposteOS.Infrastructure.Sourcing;

public sealed record SourceOpportunityReference
{
    public SourceOpportunityReference(string source, string sourceId)
    {
        Source = SourcingSource.Normalize(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceId);
        SourceId = sourceId.Trim();
    }

    public string Source { get; }

    public string SourceId { get; }
}
