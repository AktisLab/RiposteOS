using Gridify;
using RiposteOS.Core.Sourcing;

namespace RiposteOS.Infrastructure.Sourcing;

internal sealed class OpportunityGridifyMapper : GridifyMapper<Opportunity>
{
    public OpportunityGridifyMapper()
    {
        AddMap("id", opportunity => opportunity.Id);
        AddMap("source", opportunity => opportunity.Source);
        AddMap("sourceId", opportunity => opportunity.SourceId);
        AddMap("title", opportunity => opportunity.Title);
        AddMap("buyer", opportunity => opportunity.Buyer);
        AddMap("matchScore", opportunity => opportunity.MatchScore);
        AddMap("status", opportunity => opportunity.Status);
        AddMap("publicationDate", opportunity => opportunity.PublicationDate);
        AddMap("responseDeadline", opportunity => opportunity.ResponseDeadline);
        AddMap("updatedAt", opportunity => opportunity.UpdatedAt);
    }
}
