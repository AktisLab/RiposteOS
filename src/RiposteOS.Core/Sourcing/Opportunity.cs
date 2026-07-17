using System.Buffers;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;

namespace RiposteOS.Core.Sourcing;

public sealed class Opportunity
{
    private string[] _countryCodes = [];
    private string[] _departmentCodes = [];
    private string[] _cpvCodes = [];
    private string[] _descriptorCodes = [];
    private string[] _descriptorLabels = [];
    private string[] _matchReasons = [];
    private readonly List<OpportunityPublication> _publications = [];

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
        string contentHash,
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
        ContentHash = contentHash;
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
        IEnumerable<string> countryCodes,
        IEnumerable<string> departmentCodes,
        IEnumerable<string> cpvCodes,
        IEnumerable<string> descriptorCodes,
        IEnumerable<string> descriptorLabels,
        int matchScore,
        IEnumerable<string> matchReasons,
        string noticeUrl,
        string rawPayload,
        DateTimeOffset importedAt,
        string? description = null,
        string? procedureType = null,
        string? contractNature = null,
        decimal? estimatedValue = null,
        string? currency = null,
        string? executionDuration = null,
        string? documentUrl = null)
    {
        Id = Guid.Empty;
        Source = SourcingSource.Normalize(source);
        SourceId = NormalizeRequired(sourceId, nameof(sourceId));
        Title = NormalizeRequired(title, nameof(title));
        Buyer = NormalizeRequired(buyer, nameof(buyer));
        MatchScore = ValidateScore(matchScore);
        Status = OpportunityStatus.ToQualify;
        PublicationDate = publicationDate;
        ResponseDeadline = responseDeadline;
        _countryCodes = NormalizeValues(countryCodes, nameof(countryCodes));
        _departmentCodes = NormalizeValues(departmentCodes, nameof(departmentCodes));
        _cpvCodes = NormalizeValues(cpvCodes, nameof(cpvCodes));
        _descriptorCodes = NormalizeValues(descriptorCodes, nameof(descriptorCodes));
        _descriptorLabels = NormalizeValues(descriptorLabels, nameof(descriptorLabels));
        _matchReasons = NormalizeValues(matchReasons, nameof(matchReasons));
        NoticeUrl = NormalizeOptional(noticeUrl);
        Description = NormalizeNullable(description);
        ProcedureType = NormalizeNullable(procedureType);
        ContractNature = NormalizeNullable(contractNature);
        EstimatedValue = ValidateEstimatedValue(estimatedValue);
        Currency = NormalizeNullable(currency);
        ExecutionDuration = NormalizeNullable(executionDuration);
        DocumentUrl = NormalizeNullable(documentUrl);
        RawPayload = NormalizeRequired(rawPayload, nameof(rawPayload));
        ContentHash = ComputeContentHash(
            Title,
            Buyer,
            PublicationDate,
            ResponseDeadline,
            _departmentCodes,
            _cpvCodes,
            _descriptorCodes,
            _descriptorLabels,
            Description,
            ProcedureType,
            ContractNature,
            EstimatedValue,
            Currency,
            ExecutionDuration,
            DocumentUrl,
            NoticeUrl,
            RawPayload);
        ImportedAt = importedAt;
        UpdatedAt = importedAt;
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

    public IReadOnlyList<string> CountryCodes => Array.AsReadOnly(_countryCodes);

    public IReadOnlyList<string> DepartmentCodes => Array.AsReadOnly(_departmentCodes);

    public IReadOnlyList<string> CpvCodes => Array.AsReadOnly(_cpvCodes);

    public IReadOnlyList<string> DescriptorCodes => Array.AsReadOnly(_descriptorCodes);

    public IReadOnlyList<string> DescriptorLabels => Array.AsReadOnly(_descriptorLabels);

    public IReadOnlyList<string> MatchReasons => Array.AsReadOnly(_matchReasons);

    public IReadOnlyCollection<OpportunityPublication> Publications => _publications.AsReadOnly();

    public string? Description { get; private set; }

    public string? ProcedureType { get; private set; }

    public string? ContractNature { get; private set; }

    public decimal? EstimatedValue { get; private set; }

    public string? Currency { get; private set; }

    public string? ExecutionDuration { get; private set; }

    public string? DocumentUrl { get; private set; }

    public string NoticeUrl { get; private set; }

    public string RawPayload { get; private set; }

    public string ContentHash { get; private set; }

    public DateTimeOffset ImportedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public bool RefreshFromSource(
        string title,
        string buyer,
        DateOnly publicationDate,
        DateTimeOffset? responseDeadline,
        IEnumerable<string> countryCodes,
        IEnumerable<string> departmentCodes,
        IEnumerable<string> cpvCodes,
        IEnumerable<string> descriptorCodes,
        IEnumerable<string> descriptorLabels,
        int matchScore,
        IEnumerable<string> matchReasons,
        string noticeUrl,
        string rawPayload,
        DateTimeOffset updatedAt,
        string? description = null,
        string? procedureType = null,
        string? contractNature = null,
        decimal? estimatedValue = null,
        string? currency = null,
        string? executionDuration = null,
        string? documentUrl = null)
    {
        if (updatedAt < ImportedAt)
        {
            throw new ArgumentOutOfRangeException(nameof(updatedAt), "The update cannot predate the import.");
        }

        var normalizedTitle = NormalizeRequired(title, nameof(title));
        var normalizedBuyer = NormalizeRequired(buyer, nameof(buyer));
        var normalizedCountryCodes = NormalizeValues(countryCodes, nameof(countryCodes));
        var normalizedDepartmentCodes = NormalizeValues(departmentCodes, nameof(departmentCodes));
        var normalizedCpvCodes = NormalizeValues(cpvCodes, nameof(cpvCodes));
        var normalizedDescriptorCodes = NormalizeValues(descriptorCodes, nameof(descriptorCodes));
        var normalizedDescriptorLabels = NormalizeValues(descriptorLabels, nameof(descriptorLabels));
        var normalizedMatchScore = ValidateScore(matchScore);
        var normalizedMatchReasons = NormalizeValues(matchReasons, nameof(matchReasons));
        var normalizedNoticeUrl = NormalizeOptional(noticeUrl);
        var normalizedDescription = NormalizeNullable(description);
        var normalizedProcedureType = NormalizeNullable(procedureType);
        var normalizedContractNature = NormalizeNullable(contractNature);
        var normalizedEstimatedValue = ValidateEstimatedValue(estimatedValue);
        var normalizedCurrency = NormalizeNullable(currency);
        var normalizedExecutionDuration = NormalizeNullable(executionDuration);
        var normalizedDocumentUrl = NormalizeNullable(documentUrl);
        var normalizedRawPayload = NormalizeRequired(rawPayload, nameof(rawPayload));
        var contentHash = ComputeContentHash(
            normalizedTitle,
            normalizedBuyer,
            publicationDate,
            responseDeadline,
            normalizedDepartmentCodes,
            normalizedCpvCodes,
            normalizedDescriptorCodes,
            normalizedDescriptorLabels,
            normalizedDescription,
            normalizedProcedureType,
            normalizedContractNature,
            normalizedEstimatedValue,
            normalizedCurrency,
            normalizedExecutionDuration,
            normalizedDocumentUrl,
            normalizedNoticeUrl,
            normalizedRawPayload);

        var currentContentHash = GetCurrentContentHash();
        if (string.Equals(currentContentHash, contentHash, StringComparison.Ordinal))
        {
            _countryCodes = normalizedCountryCodes;
            ContentHash = currentContentHash;
            return false;
        }

        Title = normalizedTitle;
        Buyer = normalizedBuyer;
        PublicationDate = publicationDate;
        ResponseDeadline = responseDeadline;
        _countryCodes = normalizedCountryCodes;
        _departmentCodes = normalizedDepartmentCodes;
        _cpvCodes = normalizedCpvCodes;
        _descriptorCodes = normalizedDescriptorCodes;
        _descriptorLabels = normalizedDescriptorLabels;
        MatchScore = normalizedMatchScore;
        _matchReasons = normalizedMatchReasons;
        Description = normalizedDescription;
        ProcedureType = normalizedProcedureType;
        ContractNature = normalizedContractNature;
        EstimatedValue = normalizedEstimatedValue;
        Currency = normalizedCurrency;
        ExecutionDuration = normalizedExecutionDuration;
        DocumentUrl = normalizedDocumentUrl;
        NoticeUrl = normalizedNoticeUrl;
        RawPayload = normalizedRawPayload;
        ContentHash = contentHash;
        UpdatedAt = updatedAt;
        return true;
    }

    internal string GetCurrentContentHash() =>
        string.IsNullOrEmpty(ContentHash)
            ? ComputeContentHash(
                Title,
                Buyer,
                PublicationDate,
                ResponseDeadline,
                _departmentCodes,
                _cpvCodes,
                _descriptorCodes,
                _descriptorLabels,
                Description,
                ProcedureType,
                ContractNature,
                EstimatedValue,
                Currency,
                ExecutionDuration,
                DocumentUrl,
                NoticeUrl,
                RawPayload)
            : ContentHash;

    public void Retain() => Status = OpportunityStatus.Retained;

    public void Dismiss() => Status = OpportunityStatus.Dismissed;

    public void ReturnToQualification() => Status = OpportunityStatus.ToQualify;

    public OpportunityPublication AddPublication(
        string source,
        string sourceId,
        string noticeUrl,
        string? documentUrl,
        string rawPayload,
        DateTimeOffset firstSeenAt)
    {
        var normalizedSource = SourcingSource.Normalize(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceId);
        var normalizedSourceId = sourceId.Trim();
        if (_publications.Any(publication =>
            publication.Source == normalizedSource
            && publication.SourceId == normalizedSourceId))
        {
            throw new InvalidOperationException("The opportunity already contains this publication.");
        }

        var publication = new OpportunityPublication(
            this,
            normalizedSource,
            normalizedSourceId,
            noticeUrl,
            documentUrl,
            rawPayload,
            firstSeenAt);
        _publications.Add(publication);
        return publication;
    }

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

    private static string? NormalizeNullable(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static decimal? ValidateEstimatedValue(decimal? value)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "The estimated value cannot be negative.");
        }

        return value;
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

    private static string ComputeContentHash(
        string title,
        string buyer,
        DateOnly publicationDate,
        DateTimeOffset? responseDeadline,
        string[] departmentCodes,
        string[] cpvCodes,
        string[] descriptorCodes,
        string[] descriptorLabels,
        string? description,
        string? procedureType,
        string? contractNature,
        decimal? estimatedValue,
        string? currency,
        string? executionDuration,
        string? documentUrl,
        string noticeUrl,
        string rawPayload)
    {
        using var payload = JsonDocument.Parse(rawPayload);
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("title", title);
            writer.WriteString("buyer", buyer);
            writer.WriteString(
                "publicationDate",
                publicationDate.ToString("O", CultureInfo.InvariantCulture));
            if (responseDeadline is { } deadline)
            {
                writer.WriteString("responseDeadline", deadline);
            }
            else
            {
                writer.WriteNull("responseDeadline");
            }

            WriteSortedValues(writer, "departmentCodes", departmentCodes);
            WriteSortedValues(writer, "cpvCodes", cpvCodes);
            WriteSortedValues(writer, "descriptorCodes", descriptorCodes);
            WriteSortedValues(writer, "descriptorLabels", descriptorLabels);
            writer.WriteString("description", description);
            writer.WriteString("procedureType", procedureType);
            writer.WriteString("contractNature", contractNature);
            if (estimatedValue is { } value)
            {
                writer.WriteNumber("estimatedValue", value);
            }
            else
            {
                writer.WriteNull("estimatedValue");
            }

            writer.WriteString("currency", currency);
            writer.WriteString("executionDuration", executionDuration);
            writer.WriteString("documentUrl", documentUrl);
            writer.WriteString("noticeUrl", noticeUrl);
            writer.WritePropertyName("rawPayload");
            WriteCanonicalJson(writer, payload.RootElement);
            writer.WriteEndObject();
        }

        return Convert.ToHexString(SHA256.HashData(buffer.WrittenSpan)).ToLowerInvariant();
    }

    private static void WriteSortedValues(Utf8JsonWriter writer, string propertyName, string[] values)
    {
        writer.WriteStartArray(propertyName);
        foreach (var value in values.Order(StringComparer.Ordinal))
        {
            writer.WriteStringValue(value);
        }

        writer.WriteEndArray();
    }

    private static void WriteCanonicalJson(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject().OrderBy(item => item.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonicalJson(writer, property.Value);
                }

                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                {
                    WriteCanonicalJson(writer, item);
                }

                writer.WriteEndArray();
                break;
            case JsonValueKind.String:
                writer.WriteStringValue(element.GetString());
                break;
            case JsonValueKind.Number:
                writer.WriteRawValue(element.GetRawText());
                break;
            case JsonValueKind.True:
                writer.WriteBooleanValue(true);
                break;
            case JsonValueKind.False:
                writer.WriteBooleanValue(false);
                break;
            case JsonValueKind.Null:
                writer.WriteNullValue();
                break;
            default:
                throw new JsonException("Unsupported JSON value in an opportunity payload.");
        }
    }
}
