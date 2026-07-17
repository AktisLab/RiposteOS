using RiposteOS.Core.Sourcing;

namespace RiposteOS.Infrastructure.Sourcing;

public interface IOpportunitySource
{
    string Key { get; }

    DateOnly GetStartDate(DateOnly today, DateOnly? lastSuccessfulDate);

    SourceOpportunity ParseRawOpportunity(string rawPayload) =>
        throw new NotSupportedException($"Source '{Key}' does not support issue reprocessing.");

    IAsyncEnumerable<SourcingPage> ReadPagesAsync(
        SourcingSettings settings,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken);
}
