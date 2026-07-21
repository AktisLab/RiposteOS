using System.Diagnostics;
using RiposteOS.Infrastructure.Ai.Tasks;

namespace RiposteOS.Infrastructure.Ai.Execution;

public sealed class AiExecutionScope(
    AiExecutionRecorder recorder,
    Guid id,
    Activity? activity) : IDisposable
{
    private bool finished;

    public Guid Id => id;

    public async Task SetProviderAsync(AiTaskClient client, CancellationToken cancellationToken)
    {
        await recorder.SetProviderAsync(id, client.ProviderId, client.ProviderName, client.Model, cancellationToken);
        activity?.SetTag("gen_ai.provider.name", client.ProviderName);
        activity?.SetTag("gen_ai.request.model", client.Model);
    }

    public Task RecordInputAsync(string input, CancellationToken cancellationToken) =>
        recorder.RecordInputAsync(id, input, cancellationToken);

    public Task RecordOutputAsync(string output, CancellationToken cancellationToken) =>
        recorder.RecordOutputAsync(id, output, cancellationToken);

    public async Task CompleteAsync(CancellationToken cancellationToken)
    {
        await recorder.CompleteAsync(id, cancellationToken);
        finished = true;
    }

    public async Task FailAsync(
        string message,
        bool notConfigured,
        CancellationToken cancellationToken)
    {
        await recorder.FailAsync(id, message, notConfigured, cancellationToken);
        activity?.SetStatus(ActivityStatusCode.Error, notConfigured ? "not_configured" : "failed");
        finished = true;
    }

    public void Dispose()
    {
        if (!finished) activity?.SetStatus(ActivityStatusCode.Error, "incomplete");
        activity?.Dispose();
    }
}
