using System.Text.Json;

namespace RiposteOS.Core.Ai;

public sealed class AiExecutionPayload
{
    private AiExecutionPayload(Guid executionId, string input, string? output)
    {
        ExecutionId = executionId;
        Input = input;
        Output = output;
    }

    public AiExecutionPayload(Guid executionId, string input)
        : this(RequiredId(executionId), RequiredJson(input), null)
    {
    }

    public Guid ExecutionId { get; private set; }

    public string Input { get; private set; }

    public string? Output { get; private set; }

    public void RecordOutput(string output)
    {
        if (Output is not null)
        {
            throw new InvalidOperationException("The execution output is already recorded.");
        }

        Output = RequiredJson(output);
    }

    private static Guid RequiredId(Guid value) =>
        value == Guid.Empty
            ? throw new ArgumentException("An execution identifier is required.", nameof(value))
            : value;

    private static string RequiredJson(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        using var _ = JsonDocument.Parse(value);
        return value;
    }
}
