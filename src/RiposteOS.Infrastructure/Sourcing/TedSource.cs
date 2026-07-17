using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RiposteOS.Core.Sourcing;

namespace RiposteOS.Infrastructure.Sourcing;

public sealed partial class TedSource(
    HttpClient httpClient,
    IOptions<TedOptions> options) : IOpportunitySource
{
    private const string OpportunityNoticeTypes =
        "pin-cfc-standard pin-cfc-social pin-rtl pin-tran cn-standard cn-social cn-desg subco pmc qu-sy";

    private static readonly string[] RequestedFields =
    [
        "publication-number",
        "notice-identifier",
        "notice-title",
        "buyer-name",
        "publication-date",
        "deadline-receipt-tender-date-lot",
        "deadline-receipt-tender-time-lot",
        "deadline-receipt-request-date-lot",
        "deadline-receipt-request-time-lot",
        "deadline-receipt-expressions-date-lot",
        "deadline-receipt-expressions-time-lot",
        "classification-cpv",
        "description-proc",
        "description-lot",
        "procedure-type",
        "contract-nature",
        "estimated-value-proc",
        "estimated-value-cur-proc",
        "estimated-value-lot",
        "estimated-value-cur-lot",
        "duration-period-value-lot",
        "duration-period-unit-lot",
        "document-url-lot",
        "document-restricted-url-lot",
        "place-of-performance-country-lot",
        "place-of-performance-subdiv-lot",
        "place-of-performance-post-code-lot",
        "links",
    ];

    public string Key => SourcingSource.Ted;

    public DateOnly GetStartDate(DateOnly today, DateOnly? lastSuccessfulDate) =>
        lastSuccessfulDate?.AddDays(-options.Value.OverlapDays)
        ?? today.AddDays(-options.Value.InitialLookbackDays);

    public async IAsyncEnumerable<SourcingPage> ReadPagesAsync(
        SourcingSettings settings,
        DateOnly startDate,
        DateOnly endDate,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var cpvPrefixes = settings.CpvWhitelistPrefixes
            .Concat(settings.CpvWatchPrefixes)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        for (var publicationDate = startDate; publicationDate <= endDate; publicationDate = publicationDate.AddDays(1))
        {
            var pageNumber = 1;

            while (true)
            {
                var page = await SearchPageAsync(
                    settings.Keywords,
                    settings.ExcludedKeywords,
                    cpvPrefixes,
                    settings.AllowedCountryCodes,
                    publicationDate,
                    pageNumber,
                    settings.PageSize,
                    cancellationToken);
                yield return new SourcingPage(
                    page.PublicationDate,
                    page.Fetched,
                    page.Opportunities,
                    page.Skipped,
                    page.Issues);

                if (page.Fetched == 0 || pageNumber * settings.PageSize >= page.TotalCount)
                {
                    break;
                }

                pageNumber++;
            }
        }
    }

    public async Task<TedPage> SearchPageAsync(
        IReadOnlyCollection<string> keywords,
        IReadOnlyCollection<string> excludedKeywords,
        IReadOnlyCollection<string> cpvPrefixes,
        IReadOnlyCollection<string> allowedCountryCodes,
        DateOnly publicationDate,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var included = NormalizeTerms(keywords);
        var includedCpvPrefixes = NormalizeTerms(cpvPrefixes)
            .Where(prefix => prefix.Length is >= 2 and <= 8 && prefix.All(char.IsDigit))
            .ToArray();
        if (included.Length == 0 && includedCpvPrefixes.Length == 0)
        {
            return new TedPage(publicationDate, 0, 0, [], 0);
        }

        var collectionQuery = new List<string>(2);
        if (included.Length > 0)
        {
            collectionQuery.Add($"notice-title IN ({FormatTerms(included)})");
        }

        if (includedCpvPrefixes.Length > 0)
        {
            collectionQuery.Add($"classification-cpv IN ({string.Join(' ', includedCpvPrefixes.Select(FormatCpvPrefix))})");
        }

        var excluded = NormalizeTerms(excludedKeywords);
        var query = $"publication-date={publicationDate:yyyyMMdd} AND notice-type IN ({OpportunityNoticeTypes}) AND ({string.Join(" OR ", collectionQuery)})";
        if (excluded.Length > 0)
        {
            query += $" AND notice-title NOT IN ({FormatTerms(excluded)})";
        }

        var countries = NormalizeTerms(allowedCountryCodes);
        if (countries.Length > 0)
        {
            query += $" AND place-of-performance-country-lot IN ({string.Join(' ', countries)})";
        }

        using var response = await httpClient.PostAsJsonAsync(
            "v3/notices/search",
            new
            {
                query,
                fields = RequestedFields,
                page = Math.Max(pageNumber, 1),
                limit = Math.Clamp(pageSize, 1, 100),
                scope = "ALL",
                paginationMode = "PAGE_NUMBER",
                onlyLatestVersions = true,
            },
            cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var notices = document.RootElement.GetProperty("notices");
        var opportunities = new List<SourceOpportunity>();
        var issues = new List<SourceImportIssue>();
        var skipped = 0;

        foreach (var notice in notices.EnumerateArray())
        {
            try
            {
                opportunities.Add(MapOpportunity(notice));
            }
            catch (JsonException)
            {
                skipped++;
                issues.Add(CreateIssue(notice, "mapping_json"));
            }
            catch (FormatException)
            {
                skipped++;
                issues.Add(CreateIssue(notice, "mapping_format"));
            }
        }

        return new TedPage(
            publicationDate,
            notices.GetArrayLength(),
            document.RootElement.GetProperty("totalNoticeCount").GetInt32(),
            opportunities,
            skipped,
            issues);
    }

    public SourceOpportunity ParseRawOpportunity(string rawPayload)
    {
        using var document = JsonDocument.Parse(rawPayload);
        return MapOpportunity(document.RootElement);
    }

    private static SourceImportIssue CreateIssue(JsonElement notice, string errorCode) =>
        new(GetString(notice, "publication-number"), errorCode, notice.GetRawText());

    private static string[] NormalizeTerms(IEnumerable<string> terms) =>
        terms.Where(term => !string.IsNullOrWhiteSpace(term))
            .Select(term => term.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static string FormatTerms(IEnumerable<string> terms) =>
        string.Join(' ', terms.Select(term => $"\"{EscapeQueryValue(term)}\""));

    private static string FormatCpvPrefix(string prefix) =>
        prefix.Length == 8 ? prefix : $"{prefix}*";

    private static string EscapeQueryValue(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
}
