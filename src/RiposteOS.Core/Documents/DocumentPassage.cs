namespace RiposteOS.Core.Documents;

public sealed class DocumentPassage
{
    public const int MaximumSectionTitleLength = 1_000;
    public const int MaximumSourceLocationLength = 1_000;

    private DocumentPassage(
        Guid id,
        Guid documentProcessingRunId,
        int ordinal,
        string text,
        int? pageNumber,
        string? sectionTitle,
        string? sourceLocation)
    {
        Id = id;
        DocumentProcessingRunId = documentProcessingRunId;
        Ordinal = ordinal;
        Text = text;
        PageNumber = pageNumber;
        SectionTitle = sectionTitle;
        SourceLocation = sourceLocation;
    }

    public DocumentPassage(
        Guid documentProcessingRunId,
        int ordinal,
        string text,
        int? pageNumber,
        string? sectionTitle,
        string? sourceLocation)
        : this(
            Guid.Empty,
            ValidateIdentifier(documentProcessingRunId, nameof(documentProcessingRunId)),
            ValidateOrdinal(ordinal),
            NormalizeRequired(text, nameof(text)),
            ValidatePageNumber(pageNumber),
            NormalizeOptional(sectionTitle, MaximumSectionTitleLength, nameof(sectionTitle)),
            NormalizeOptional(sourceLocation, MaximumSourceLocationLength, nameof(sourceLocation)))
    {
    }

    public Guid Id { get; private set; }

    public Guid DocumentProcessingRunId { get; private set; }

    public DocumentProcessingRun? DocumentProcessingRun { get; private set; }

    public int Ordinal { get; private set; }

    public string Text { get; private set; }

    public int? PageNumber { get; private set; }

    public string? SectionTitle { get; private set; }

    public string? SourceLocation { get; private set; }

    private static Guid ValidateIdentifier(Guid value, string parameterName) =>
        value == Guid.Empty
            ? throw new ArgumentException("An identifier is required.", parameterName)
            : value;

    private static int ValidateOrdinal(int value) => value <= 0
        ? throw new ArgumentOutOfRangeException(nameof(value), "Passage ordinal must be positive.")
        : value;

    private static int? ValidatePageNumber(int? value) => value is <= 0
        ? throw new ArgumentOutOfRangeException(nameof(value), "Page number must be positive.")
        : value;

    private static string NormalizeRequired(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        return value.Trim();
    }

    private static string? NormalizeOptional(string? value, int maximumLength, string parameterName)
    {
        if (value is null)
        {
            return null;
        }

        var normalized = value.Trim();
        if (normalized.Length == 0 || normalized.Length > maximumLength)
        {
            throw new ArgumentException("The value is invalid.", parameterName);
        }

        return normalized;
    }
}
