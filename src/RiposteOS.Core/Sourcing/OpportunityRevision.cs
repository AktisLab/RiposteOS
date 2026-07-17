namespace RiposteOS.Core.Sourcing;

public sealed class OpportunityRevision
{
    private OpportunityRevision(
        Guid id,
        Guid opportunityId,
        string contentHash,
        string rawPayload,
        DateTimeOffset createdAt)
    {
        Id = id;
        OpportunityId = opportunityId;
        ContentHash = contentHash;
        RawPayload = rawPayload;
        CreatedAt = createdAt;
    }

    public OpportunityRevision(Opportunity opportunity, DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(opportunity);
        if (createdAt < opportunity.ImportedAt)
        {
            throw new ArgumentOutOfRangeException(nameof(createdAt), "The revision cannot predate the import.");
        }

        Id = Guid.Empty;
        OpportunityId = opportunity.Id;
        Opportunity = opportunity;
        ContentHash = opportunity.GetCurrentContentHash();
        RawPayload = opportunity.RawPayload;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid OpportunityId { get; private set; }

    public Opportunity? Opportunity { get; private set; }

    public string ContentHash { get; private set; }

    public string RawPayload { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public void ReassignTo(Opportunity opportunity)
    {
        ArgumentNullException.ThrowIfNull(opportunity);
        Opportunity = opportunity;
        OpportunityId = opportunity.Id;
    }
}
