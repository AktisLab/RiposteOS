using RiposteOS.Core.Sourcing;

namespace RiposteOS.Infrastructure.Sourcing;

public sealed record OpportunityPageResult(
    Opportunity[] Items,
    int TotalCount,
    string[] ValidationErrors);
