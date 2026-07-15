namespace RiposteOS.Core.Sourcing;

public sealed class Opportunity
{
    private string[] _departmentCodes = [];
    private string[] _cpvCodes = [];
    private string[] _descriptorCodes = [];
    private string[] _descriptorLabels = [];
    private string[] _matchReasons = [];

    private Opportunity(
        Guid id,
        string source,
        string sourceId,
        string title,
        string buyer,
        int matchScore,
        OpportunityStatus status,
        DateOnly publicationDate,
        DateTimeOffset? responseDeadline,
        string noticeUrl,
        string rawPayload,
        DateTimeOffset importedAt,
        DateTimeOffset updatedAt)
    {
        Id = id;
        Source = source;
        SourceId = sourceId;
        Title = title;
        Buyer = buyer;
        MatchScore = matchScore;
        Status = status;
        PublicationDate = publicationDate;
        ResponseDeadline = responseDeadline;
        NoticeUrl = noticeUrl;
        RawPayload = rawPayload;
        ImportedAt = importedAt;
        UpdatedAt = updatedAt;
    }

    public Opportunity(
        string source,
        string sourceId,
        string title,
        string buyer,
        DateOnly publicationDate,
        DateTimeOffset? responseDeadline,
        IEnumerable<string> departmentCodes,
        IEnumerable<string> cpvCodes,
        IEnumerable<string> descriptorCodes,
        IEnumerable<string> descriptorLabels,
        int matchScore,
        IEnumerable<string> matchReasons,
        string noticeUrl,
        string rawPayload,
        DateTimeOffset importedAt)
        : this(
            Guid.Empty,
            SourcingSource.Normalize(source),
            NormalizeRequired(sourceId, nameof(sourceId)),
            NormalizeRequired(title, nameof(title)),
            NormalizeRequired(buyer, nameof(buyer)),
            ValidateScore(matchScore),
            OpportunityStatus.ToQualify,
            publicationDate,
            responseDeadline,
            NormalizeOptional(noticeUrl),
            NormalizeRequired(rawPayload, nameof(rawPayload)),
            importedAt,
            importedAt)
    {
        _departmentCodes = NormalizeValues(departmentCodes, nameof(departmentCodes));
        _cpvCodes = NormalizeValues(cpvCodes, nameof(cpvCodes));
        _descriptorCodes = NormalizeValues(descriptorCodes, nameof(descriptorCodes));
        _descriptorLabels = NormalizeValues(descriptorLabels, nameof(descriptorLabels));
        _matchReasons = NormalizeValues(matchReasons, nameof(matchReasons));
    }

    public Guid Id { get; private set; }

    public string Source { get; private set; }

    public string SourceId { get; private set; }

    public string Title { get; private set; }

    public string Buyer { get; private set; }

    public int MatchScore { get; private set; }

    public OpportunityStatus Status { get; private set; }

    public DateOnly PublicationDate { get; private set; }

    public DateTimeOffset? ResponseDeadline { get; private set; }

    public IReadOnlyList<string> DepartmentCodes => Array.AsReadOnly(_departmentCodes);

    public IReadOnlyList<string> CpvCodes => Array.AsReadOnly(_cpvCodes);

    public IReadOnlyList<string> DescriptorCodes => Array.AsReadOnly(_descriptorCodes);

    public IReadOnlyList<string> DescriptorLabels => Array.AsReadOnly(_descriptorLabels);

    public IReadOnlyList<string> MatchReasons => Array.AsReadOnly(_matchReasons);

    public string NoticeUrl { get; private set; }

    public string RawPayload { get; private set; }

    public DateTimeOffset ImportedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public void RefreshFromSource(
        string title,
        string buyer,
        DateOnly publicationDate,
        DateTimeOffset? responseDeadline,
        IEnumerable<string> departmentCodes,
        IEnumerable<string> cpvCodes,
        IEnumerable<string> descriptorCodes,
        IEnumerable<string> descriptorLabels,
        int matchScore,
        IEnumerable<string> matchReasons,
        string noticeUrl,
        string rawPayload,
        DateTimeOffset updatedAt)
    {
        if (updatedAt < ImportedAt)
        {
            throw new ArgumentOutOfRangeException(nameof(updatedAt), "The update cannot predate the import.");
        }

        Title = NormalizeRequired(title, nameof(title));
        Buyer = NormalizeRequired(buyer, nameof(buyer));
        PublicationDate = publicationDate;
        ResponseDeadline = responseDeadline;
        _departmentCodes = NormalizeValues(departmentCodes, nameof(departmentCodes));
        _cpvCodes = NormalizeValues(cpvCodes, nameof(cpvCodes));
        _descriptorCodes = NormalizeValues(descriptorCodes, nameof(descriptorCodes));
        _descriptorLabels = NormalizeValues(descriptorLabels, nameof(descriptorLabels));
        MatchScore = ValidateScore(matchScore);
        _matchReasons = NormalizeValues(matchReasons, nameof(matchReasons));
        NoticeUrl = NormalizeOptional(noticeUrl);
        RawPayload = NormalizeRequired(rawPayload, nameof(rawPayload));
        UpdatedAt = updatedAt;
    }

    public void Retain() => Status = OpportunityStatus.Retained;

    public void Dismiss() => Status = OpportunityStatus.Dismissed;

    public void ReturnToQualification() => Status = OpportunityStatus.ToQualify;

    public void ReassessMatch(
        int matchScore,
        IEnumerable<string> matchReasons,
        DateTimeOffset reassessedAt)
    {
        if (reassessedAt < UpdatedAt)
        {
            throw new ArgumentOutOfRangeException(
                nameof(reassessedAt),
                "The reassessment cannot predate the latest opportunity update.");
        }

        MatchScore = ValidateScore(matchScore);
        _matchReasons = NormalizeValues(matchReasons, nameof(matchReasons));
        UpdatedAt = reassessedAt;
    }

    private static string NormalizeRequired(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        return value.Trim();
    }

    private static string NormalizeOptional(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value.Trim();
    }

    private static string[] NormalizeValues(IEnumerable<string> values, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(values, parameterName);
        return values
            .Select(value => NormalizeRequired(value, parameterName))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static int ValidateScore(int score)
    {
        if (score is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(score), "The match score must be between 0 and 100.");
        }

        return score;
    }
}
