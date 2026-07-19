namespace RiposteOS.Infrastructure.Documents;

public interface IDocumentParser
{
    Task<ParsedDocument> ParseAsync(
        string fileName,
        string contentType,
        Stream content,
        CancellationToken cancellationToken);
}

public sealed record ParsedDocument(int PageCount, IReadOnlyList<ParsedPassage> Passages);

public sealed record ParsedPassage(
    string Text,
    int? PageNumber,
    string? SectionTitle,
    string? SourceLocation);
