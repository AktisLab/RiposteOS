using RiposteOS.Core.Sourcing;

namespace RiposteOS.Infrastructure.Sourcing;

public sealed record ImportQueueResult(ImportRun? Run, bool Created);

public sealed record OpportunityStatusUpdateResult(
    Opportunity? Opportunity,
    bool ConsultationConflict);
