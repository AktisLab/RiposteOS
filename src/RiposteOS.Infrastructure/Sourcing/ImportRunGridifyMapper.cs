using Gridify;
using RiposteOS.Core.Sourcing;

namespace RiposteOS.Infrastructure.Sourcing;

internal sealed class ImportRunGridifyMapper : GridifyMapper<ImportRun>
{
    public ImportRunGridifyMapper()
    {
        AddMap("id", run => run.Id);
        AddMap("queuedAt", run => run.QueuedAt);
    }
}
