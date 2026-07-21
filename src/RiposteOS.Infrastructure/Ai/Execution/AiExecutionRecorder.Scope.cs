using RiposteOS.Core.Ai;

namespace RiposteOS.Infrastructure.Ai.Execution;

public sealed partial class AiExecutionRecorder
{
    public async Task<AiExecutionScope> StartScopeAsync(
        AiExecutionStart start,
        CancellationToken cancellationToken)
    {
        var id = await StartAsync(start, cancellationToken);
        var activity = AiExecutionTelemetry.Start(start.Operation);
        activity?.SetTag("gen_ai.operation.name", start.Operation switch
        {
            AiExecutionOperation.DocumentAnalysis => "document_processing",
            AiExecutionOperation.DocumentEmbedding => "embeddings",
            _ => "chat",
        });
        activity?.SetTag("gen_ai.provider.name", start.ProviderName);
        activity?.SetTag("gen_ai.request.model", start.Model);
        return new AiExecutionScope(this, id, activity);
    }
}
