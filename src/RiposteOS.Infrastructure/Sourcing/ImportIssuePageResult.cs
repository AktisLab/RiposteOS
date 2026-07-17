using RiposteOS.Core.Sourcing;

namespace RiposteOS.Infrastructure.Sourcing;

public sealed record ImportIssuePageResult(
    IReadOnlyList<ImportIssue> Items,
    int TotalCount);
