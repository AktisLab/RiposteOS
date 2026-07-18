using RiposteOS.Core.Sourcing;

namespace RiposteOS.Core.Consultations;

public sealed class Consultation
{
    public const int MaximumTitleLength = 2_000;
    public const int MaximumBuyerLength = 1_000;
    public const int MaximumNoticeUrlLength = 2_000;

    private Consultation(
        Guid id,
        Guid? opportunityId,
        string title,
        string buyer,
        DateTimeOffset? responseDeadline,
        string? noticeUrl,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        if (updatedAt < createdAt)
        {
            throw new ArgumentOutOfRangeException(nameof(updatedAt), "The update cannot predate creation.");
        }

        Id = id;
        OpportunityId = opportunityId;
        Title = NormalizeRequired(title, MaximumTitleLength, nameof(title));
        Buyer = NormalizeRequired(buyer, MaximumBuyerLength, nameof(buyer));
        ResponseDeadline = responseDeadline;
        NoticeUrl = NormalizeNoticeUrl(noticeUrl);
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public Consultation(
        string title,
        string buyer,
        DateTimeOffset? responseDeadline,
        string? noticeUrl,
        DateTimeOffset createdAt)
        : this(Guid.Empty, null, title, buyer, responseDeadline, noticeUrl, createdAt, createdAt)
    {
    }

    public static Consultation FromOpportunity(Opportunity opportunity, DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(opportunity);
        if (opportunity.Id == Guid.Empty)
        {
            throw new ArgumentException("A persisted opportunity is required.", nameof(opportunity));
        }

        return new Consultation(
            Guid.Empty,
            opportunity.Id,
            opportunity.Title,
            opportunity.Buyer,
            opportunity.ResponseDeadline,
            opportunity.NoticeUrl,
            createdAt,
            createdAt);
    }

    public Guid Id { get; private set; }

    public Guid? OpportunityId { get; private set; }

    public string Title { get; private set; }

    public string Buyer { get; private set; }

    public DateTimeOffset? ResponseDeadline { get; private set; }

    public string? NoticeUrl { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public void ReassignToOpportunity(Guid opportunityId, DateTimeOffset updatedAt)
    {
        if (opportunityId == Guid.Empty)
        {
            throw new ArgumentException("An opportunity identifier is required.", nameof(opportunityId));
        }

        if (updatedAt < UpdatedAt)
        {
            throw new ArgumentOutOfRangeException(nameof(updatedAt), "The update cannot predate the previous update.");
        }

        OpportunityId = opportunityId;
        UpdatedAt = updatedAt;
    }

    private static string NormalizeRequired(string value, int maximumLength, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        var normalized = value.Trim();
        if (normalized.Length > maximumLength)
        {
            throw new ArgumentException($"The value cannot exceed {maximumLength} characters.", parameterName);
        }

        return normalized;
    }

    private static string? NormalizeNoticeUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        if (normalized.Length > MaximumNoticeUrlLength
            || !Uri.TryCreate(normalized, UriKind.Absolute, out var uri)
            || (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException("The notice URL must be absolute.", nameof(value));
        }

        return normalized;
    }
}
