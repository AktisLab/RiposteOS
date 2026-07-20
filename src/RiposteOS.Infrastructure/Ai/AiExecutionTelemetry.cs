using System.Diagnostics;
using RiposteOS.Core.Ai;

namespace RiposteOS.Infrastructure.Ai;

public static class AiExecutionTelemetry
{
    public const string SourceName = "RiposteOS.Ai";
    public static readonly ActivitySource Source = new(SourceName);

    public static Activity? Start(AiExecutionOperation operation) =>
        Source.StartActivity($"ai.{operation}", ActivityKind.Client);
}
