using System.Security.Cryptography;
using System.Text;

namespace RiposteOS.Core.Sourcing;

public sealed class OpportunityPublication
{
    private OpportunityPublication(
        Guid id,
        Guid opportunityId,
        string source,
        string sourceId,
        string noticeUrl,
        string documentUrl,
        string rawPayload,
        string contentHash,
        DateTimeOffset firstSeenAt,
        DateTimeOffset updatedAt)
    {
        Id = id;
        OpportunityId = opportunityId;
        Source = source;
        SourceId = sourceId;
        NoticeUrl = noticeUrl;
        DocumentUrl = documentUrl;
        RawPayload = rawPayload;
        ContentHash = contentHash;
        FirstSeenAt = firstSeenAt;
        UpdatedAt = updatedAt;
    }

    internal OpportunityPublication(
        Opportunity opportunity,
        string source,
        string sourceId,
        string noticeUrl,
        string? documentUrl,
        string rawPayload,
        DateTimeOffset firstSeenAt)
    {
        ArgumentNullException.ThrowIfNull(opportunity);
        Id = Guid.Empty;
        OpportunityId = opportunity.Id;
        Opportunity = opportunity;
        Source = SourcingSource.Normalize(source);
        SourceId = NormalizeRequired(sourceId, nameof(sourceId));
        NoticeUrl = NormalizeOptional(noticeUrl);
        DocumentUrl = NormalizeOptional(documentUrl);
        RawPayload = NormalizeRequired(rawPayload, nameof(rawPayload));
        ContentHash = ComputeContentHash(NoticeUrl, DocumentUrl, RawPayload);
        FirstSeenAt = firstSeenAt;
        UpdatedAt = firstSeenAt;
    }

    public Guid Id { get; private set; }

    public Guid OpportunityId { get; private set; }

    public Opportunity? Opportunity { get; private set; }

    public string Source { get; private set; }

    public string SourceId { get; private set; }

    public string NoticeUrl { get; private set; }

    public string DocumentUrl { get; private set; }

    public string RawPayload { get; private set; }

    public string ContentHash { get; private set; }

    public DateTimeOffset FirstSeenAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public bool Refresh(
        string noticeUrl,
        string? documentUrl,
        string rawPayload,
        DateTimeOffset updatedAt)
    {
        if (updatedAt < FirstSeenAt)
        {
            throw new ArgumentOutOfRangeException(nameof(updatedAt), "The update cannot predate first sighting.");
        }

        var normalizedNoticeUrl = NormalizeOptional(noticeUrl);
        var normalizedDocumentUrl = NormalizeOptional(documentUrl);
        var normalizedRawPayload = NormalizeRequired(rawPayload, nameof(rawPayload));
        var contentHash = ComputeContentHash(
            normalizedNoticeUrl,
            normalizedDocumentUrl,
            normalizedRawPayload);
        if (string.Equals(ContentHash, contentHash, StringComparison.Ordinal))
        {
            return false;
        }

        NoticeUrl = normalizedNoticeUrl;
        DocumentUrl = normalizedDocumentUrl;
        RawPayload = normalizedRawPayload;
        ContentHash = contentHash;
        UpdatedAt = updatedAt;
        return true;
    }

    private static string NormalizeRequired(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        return value.Trim();
    }

    private static string NormalizeOptional(string? value) => value?.Trim() ?? string.Empty;

    private static string ComputeContentHash(string noticeUrl, string documentUrl, string rawPayload) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{noticeUrl}\n{documentUrl}\n{rawPayload}"))).ToLowerInvariant();
}
