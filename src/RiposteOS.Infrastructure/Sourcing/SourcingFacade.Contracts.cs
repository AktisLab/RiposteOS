using RiposteOS.Core.Sourcing;

namespace RiposteOS.Infrastructure.Sourcing;

public sealed record ImportQueueResult(ImportRun? Run, bool Created);
