using RiposteOS.Core.Sourcing;

namespace RiposteOS.Infrastructure.Sourcing;

public sealed record ImportRunPageResult(ImportRun[] Items, int TotalCount);
