using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using RiposteOS.Infrastructure.Ai.Execution;

namespace RiposteOS.Infrastructure.Ai.Runtime;

public sealed class AiChatClientPipeline(ILoggerFactory loggerFactory)
{
    public IChatClient CreateForTools(
        IChatClient client,
        int maximumIterations,
        bool allowConcurrentInvocation = false)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumIterations, 1);

        return client.AsBuilder()
            .UseOpenTelemetry(loggerFactory, AiExecutionTelemetry.SourceName, telemetry => telemetry.EnableSensitiveData = false)
            .UseFunctionInvocation(loggerFactory, options =>
            {
                options.MaximumIterationsPerRequest = maximumIterations;
                options.AllowConcurrentInvocation = allowConcurrentInvocation;
                options.IncludeDetailedErrors = false;
            })
            .Build();
    }
}
