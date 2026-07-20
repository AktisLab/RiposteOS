namespace RiposteOS.Core.Ai;

public sealed record AiExecutionSubject(AiExecutionSubjectKind Kind, Guid Id, string Label)
{
    public const int MaximumLabelLength = 255;

    public AiExecutionSubjectKind Kind { get; } = Enum.IsDefined(Kind)
        ? Kind
        : throw new ArgumentOutOfRangeException(nameof(Kind));

    public Guid Id { get; } = Id != Guid.Empty
        ? Id
        : throw new ArgumentException("An identifier is required.", nameof(Id));

    public string Label { get; } = Normalize(Label);

    private static string Normalize(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var normalized = value.Trim();
        return normalized.Length <= MaximumLabelLength
            ? normalized
            : throw new ArgumentOutOfRangeException(nameof(value));
    }
}
