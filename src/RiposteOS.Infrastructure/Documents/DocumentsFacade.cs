using System.Security.Cryptography;
using Gridify;
using Gridify.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RiposteOS.Core.Documents;
using RiposteOS.Infrastructure.Persistence;

namespace RiposteOS.Infrastructure.Documents;

public sealed class DocumentsFacade(
    RiposteDbContext dbContext,
    IObjectStorage objectStorage,
    IOptions<ObjectStorageOptions> options,
    TimeProvider timeProvider)
{
    private static readonly DocumentGridifyMapper DocumentMapper = new();

    public async Task<StoredDocument> UploadAsync(DocumentUpload upload, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(upload.Content);
        if (upload.Size > options.Value.MaxDocumentSizeBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(upload), "The document exceeds the configured maximum size.");
        }

        if (!upload.Content.CanSeek)
        {
            throw new ArgumentException("The document stream must be seekable.", nameof(upload));
        }

        var id = Guid.NewGuid();
        upload.Content.Position = 0;
        var sha256 = Convert.ToHexString(await SHA256.HashDataAsync(upload.Content, cancellationToken)).ToLowerInvariant();
        upload.Content.Position = 0;
        var document = new StoredDocument(
            id,
            upload.OriginalFileName,
            upload.ContentType,
            upload.Size,
            sha256,
            timeProvider.GetUtcNow());

        await objectStorage.PutAsync(
            document.StorageKey,
            upload.Content,
            document.Size,
            document.ContentType,
            cancellationToken);
        dbContext.Set<StoredDocument>().Add(document);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            try
            {
                await objectStorage.DeleteAsync(document.StorageKey, CancellationToken.None);
            }
            catch (ObjectStorageUnavailableException)
            {
                // Compensation is best effort; the object is private and unreferenced.
            }

            throw;
        }

        return document;
    }

    public Task<StoredDocument?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Set<StoredDocument>().AsNoTracking().SingleOrDefaultAsync(document => document.Id == id, cancellationToken);

    public async Task<DocumentContent?> OpenContentAsync(Guid id, CancellationToken cancellationToken)
    {
        var document = await GetAsync(id, cancellationToken);
        if (document is null)
        {
            return null;
        }

        return new DocumentContent(document, await objectStorage.OpenReadAsync(document.StorageKey, cancellationToken));
    }

    public async Task<DocumentPageResult> ListAsync(
        int page,
        int pageSize,
        string? filter,
        string? orderBy,
        CancellationToken cancellationToken)
    {
        var query = new GridifyQuery(page, pageSize, filter, string.IsNullOrWhiteSpace(orderBy) ? "createdAt desc,id" : $"{orderBy},id");
        if (!query.IsValid(DocumentMapper))
        {
            return new DocumentPageResult([], 0, ["Le filtre ou le tri demandé est invalide."]);
        }

        var documents = dbContext.Set<StoredDocument>().AsNoTracking();
        if (string.Equals(dbContext.Database.ProviderName, "Microsoft.EntityFrameworkCore.InMemory", StringComparison.Ordinal))
        {
            var inMemoryResult = (await documents.ToListAsync(cancellationToken)).AsQueryable().Gridify(query, DocumentMapper);
            return new DocumentPageResult(inMemoryResult.Data.ToArray(), inMemoryResult.Count, []);
        }

        var result = await documents.GridifyAsync(query, cancellationToken, DocumentMapper);
        return new DocumentPageResult(result.Data.ToArray(), result.Count, []);
    }
}

public sealed record DocumentUpload(string OriginalFileName, string ContentType, long Size, Stream Content);

public sealed record DocumentContent(StoredDocument Document, Stream Content);

public sealed record DocumentPageResult(StoredDocument[] Items, int TotalCount, string[] ValidationErrors);
