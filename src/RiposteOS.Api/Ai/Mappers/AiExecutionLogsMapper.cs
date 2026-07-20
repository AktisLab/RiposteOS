using Riok.Mapperly.Abstractions;
using RiposteOS.Api.Ai.Dtos;
using RiposteOS.Core.Ai;

namespace RiposteOS.Api.Ai.Mappers;

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public static partial class AiExecutionLogsMapper
{
    public static partial AiExecutionLogResponse ToResponse(AiExecutionLog execution);

    public static partial AiExecutionLogResponse[] ToResponses(IEnumerable<AiExecutionLog> executions);
}
