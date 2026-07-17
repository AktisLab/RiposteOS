using System.Text;
using System.Text.RegularExpressions;

namespace RiposteOS.Core.Documents;

public sealed partial class StoredDocument
{
    public const long MaximumSize = 1_073_741_824;
    private const int MaximumFileNameLength = 255;
    private const int MaximumContentTypeLength = 255;

    [GeneratedRegex("^[a-f0-9]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex Sha256Pattern();

    private StoredDocument(
        Guid id,
        string originalFileName,
        string contentType,
        long size,
        string sha256,
        string storageKey,
        DateTimeOffset createdAt)
    {
        Id = id;
        OriginalFileName = originalFileName;
        ContentType = contentType;
        Size = size;
        Sha256 = sha256;
        StorageKey = storageKey;
        CreatedAt = createdAt;
    }

    public StoredDocument(Guid id, string originalFileName, string contentType, long size, string sha256, DateTimeOffset createdAt)
        : this(
            id == Guid.Empty ? throw new ArgumentException("A document identifier is required.", nameof(id)) : id,
            NormalizeFileName(originalFileName),
            NormalizeContentType(contentType),
            ValidateSize(size),
            NormalizeSha256(sha256),
            $"documents/{id:N}/content",
            createdAt)
    {
    }

    public Guid Id { get; private set; }

    public string OriginalFileName { get; private set; }

    public string ContentType { get; private set; }

    public long Size { get; private set; }

    public string Sha256 { get; private set; }

    public string StorageKey { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    private static string NormalizeFileName(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var normalized = value.Trim().Normalize(NormalizationForm.FormC);
        if (normalized.Length > MaximumFileNameLength || normalized.IndexOfAny(['/', '\\']) >= 0 || normalized.Any(char.IsControl))
        {
            throw new ArgumentException("The original file name is invalid.", nameof(value));
        }

        return normalized;
    }

    private static string NormalizeContentType(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var normalized = value.Trim().ToLowerInvariant();
        if (normalized.Length > MaximumContentTypeLength || !normalized.Contains('/'))
        {
            throw new ArgumentException("The content type is invalid.", nameof(value));
        }

        return normalized;
    }

    private static long ValidateSize(long value)
    {
        if (value is <= 0 or > MaximumSize)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "The document size is invalid.");
        }

        return value;
    }

    private static string NormalizeSha256(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var normalized = value.Trim().ToLowerInvariant();
        if (!Sha256Pattern().IsMatch(normalized))
        {
            throw new ArgumentException("The SHA-256 hash is invalid.", nameof(value));
        }

        return normalized;
    }
}
