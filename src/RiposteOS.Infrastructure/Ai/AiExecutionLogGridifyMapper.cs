using Gridify;
using RiposteOS.Core.Ai;

namespace RiposteOS.Infrastructure.Ai;

internal sealed class AiExecutionLogGridifyMapper : GridifyMapper<AiExecutionLog>
{
    public AiExecutionLogGridifyMapper()
    {
        AddMap("id", log => log.Id);
        AddMap("operation", log => log.Operation);
        AddMap("status", log => log.Status);
        AddMap("subjectLabel", log => log.SubjectLabel);
        AddMap("providerName", log => log.ProviderName);
        AddMap("model", log => log.Model);
        AddMap("startedAt", log => log.StartedAt);
        AddMap("completedAt", log => log.CompletedAt);
        AddMap("failedAt", log => log.FailedAt);
    }
}
