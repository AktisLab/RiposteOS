using RiposteOS.Core.Sourcing;

namespace RiposteOS.Infrastructure.Sourcing;

public sealed record ImportIssueRetryResult(ImportIssue Issue, bool Resolved);
