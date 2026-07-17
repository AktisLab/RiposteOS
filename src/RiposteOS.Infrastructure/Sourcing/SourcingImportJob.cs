using Hangfire;
using Microsoft.Extensions.Logging;

namespace RiposteOS.Infrastructure.Sourcing;

public sealed class SourcingImportJob(
    OpportunityImporter importer,
    ImportRunStore runStore,
    ILogger<SourcingImportJob> logger)
{
    private static readonly Action<ILogger, string, Guid, Exception?> LogImportFailed =
        LoggerMessage.Define<string, Guid>(
            LogLevel.Error,
            new EventId(1, nameof(LogImportFailed)),
            "{Source} import {ImportRunId} failed");

    [AutomaticRetry(Attempts = 0)]
    public async Task ExecuteAsync(
        ImportOpportunities command,
        CancellationToken cancellationToken)
    {
        if (!await runStore.StartAsync(command.RunId, cancellationToken))
        {
            return;
        }

        try
        {
            await importer.ImportAsync(command, cancellationToken);
            await runStore.CompleteAsync(command.RunId, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            LogImportFailed(logger, command.Source, command.RunId, exception);
            await runStore.FailAsync(
                command.RunId,
                $"L'import {command.Source} a échoué. Consultez les journaux du worker pour le détail.",
                CancellationToken.None);
            throw;
        }
    }
}
