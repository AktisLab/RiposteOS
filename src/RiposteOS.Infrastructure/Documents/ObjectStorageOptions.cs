namespace RiposteOS.Infrastructure.Documents;

public sealed class ObjectStorageOptions
{
    public const string SectionName = "ObjectStorage";

    public string BucketName { get; init; } = "riposteos-documents";

    public string Region { get; init; } = "us-east-1";

    public string? ServiceUrl { get; init; }

    public bool ForcePathStyle { get; init; }

    public string? AccessKey { get; init; }

    public string? SecretKey { get; init; }

    public long MaxDocumentSizeBytes { get; init; } = 52_428_800;
}
