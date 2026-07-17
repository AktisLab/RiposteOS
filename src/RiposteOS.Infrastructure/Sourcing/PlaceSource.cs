using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using Microsoft.Extensions.Options;
using RiposteOS.Core.Sourcing;

namespace RiposteOS.Infrastructure.Sourcing;

public sealed partial class PlaceSource(
    HttpClient httpClient,
    IOptions<PlaceOptions> options) : IOpportunitySource
{
    private const string FranceCountryCode = "FRA";
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public string Key => SourcingSource.Place;

    public DateOnly GetStartDate(DateOnly today, DateOnly? lastSuccessfulDate) =>
        lastSuccessfulDate?.AddDays(-options.Value.OverlapDays)
        ?? today.AddDays(-options.Value.InitialLookbackDays);

    public async IAsyncEnumerable<SourcingPage> ReadPagesAsync(
        SourcingSettings settings,
        DateOnly startDate,
        DateOnly endDate,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (settings.AllowedCountryCodes.Count > 0
            && !settings.AllowedCountryCodes.Contains(FranceCountryCode, StringComparer.OrdinalIgnoreCase))
        {
            yield break;
        }

        var queries = settings.Keywords
            .Concat(settings.CpvWhitelistPrefixes)
            .Concat(settings.CpvWatchPrefixes)
            .Where(query => !string.IsNullOrWhiteSpace(query))
            .Select(query => query.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var importedIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var query in queries)
        {
            await foreach (var result in SearchAsync(
                               query,
                               settings.ExcludedKeywords,
                               startDate,
                               endDate,
                               importedIds,
                               cancellationToken))
            {
                yield return new SourcingPage(
                    result.PublicationDate,
                    result.Fetched,
                    result.Opportunities,
                    result.Skipped,
                    result.Issues);
            }
        }
    }

    public SourceOpportunity ParseRawOpportunity(string rawPayload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawPayload);
        using var document = JsonDocument.Parse(rawPayload);
        if (document.RootElement.TryGetProperty("searchItem", out _))
        {
            var record = JsonSerializer.Deserialize<PlaceRawRecord>(rawPayload, SerializerOptions)
                ?? throw new JsonException("The PLACE raw record is empty.");
            return MapOpportunity(record.SearchItem, record.DetailHtml);
        }

        var snapshot = JsonSerializer.Deserialize<PlaceSnapshot>(rawPayload, SerializerOptions)
            ?? throw new JsonException("The PLACE snapshot is empty.");
        return ToSourceOpportunity(snapshot, rawPayload);
    }

    private async IAsyncEnumerable<PlaceSearchResult> SearchAsync(
        string query,
        IReadOnlyCollection<string> excludedKeywords,
        DateOnly startDate,
        DateOnly endDate,
        HashSet<string> importedIds,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var requestUri = $"espace-entreprise/search?keyWord={Uri.EscapeDataString(query)}";
        using var initialResponse = await httpClient.GetAsync(requestUri, cancellationToken);
        initialResponse.EnsureSuccessStatusCode();
        var currentUri = initialResponse.RequestMessage!.RequestUri!;
        var html = await initialResponse.Content.ReadAsStringAsync(cancellationToken);
        var shouldIncreasePageSize = true;

        while (true)
        {
            var document = new HtmlParser().ParseDocument(html);
            var advertisedCount = ParseInteger(document.QuerySelector("[id$='nombreElement']")?.TextContent) ?? 0;
            if (advertisedCount > 0 && document.QuerySelectorAll(".item_consultation").Length == 0)
            {
                throw new FormatException("PLACE advertised results but its result contract could not be parsed.");
            }

            if (shouldIncreasePageSize
                && TryCreatePageSizeRequest(document, currentUri, advertisedCount, out var pageSizeUri, out var pageSizeValues))
            {
                shouldIncreasePageSize = false;
                using var pageSizeContent = new FormUrlEncodedContent(pageSizeValues);
                await DelayAsync(cancellationToken);
                using var pageSizeResponse = await httpClient.PostAsync(pageSizeUri, pageSizeContent, cancellationToken);
                pageSizeResponse.EnsureSuccessStatusCode();
                currentUri = pageSizeResponse.RequestMessage!.RequestUri!;
                html = await pageSizeResponse.Content.ReadAsStringAsync(cancellationToken);
                continue;
            }

            shouldIncreasePageSize = false;

            var items = ParseSearchItems(document, currentUri)
                .Where(item => item.PublicationDate >= startDate && item.PublicationDate <= endDate)
                .Where(item => !excludedKeywords.Any(term =>
                    SourcingMatcher.ContainsTerm($"{item.Title} {item.Description}", term)))
                .Where(item => importedIds.Add(item.SourceId))
                .ToArray();
            var opportunities = new List<SourceOpportunity>(items.Length);
            var issues = new List<SourceImportIssue>();

            foreach (var item in items)
            {
                await DelayAsync(cancellationToken);
                using var detailResponse = await httpClient.GetAsync(item.NoticeUrl, cancellationToken);
                detailResponse.EnsureSuccessStatusCode();
                var detailHtml = await detailResponse.Content.ReadAsStringAsync(cancellationToken);
                try
                {
                    opportunities.Add(MapOpportunity(item, detailHtml));
                }
                catch (FormatException)
                {
                    issues.Add(new SourceImportIssue(
                        item.SourceId,
                        "mapping_format",
                        JsonSerializer.Serialize(new PlaceRawRecord(item, detailHtml), SerializerOptions)));
                }
            }

            yield return new PlaceSearchResult(
                items.Select(item => item.PublicationDate).DefaultIfEmpty(endDate).Max(),
                items.Length,
                opportunities,
                issues.Count,
                issues);

            if (!TryCreateNextPageRequest(document, currentUri, out var nextPageUri, out var formValues))
            {
                yield break;
            }

            using var content = new FormUrlEncodedContent(formValues);
            await DelayAsync(cancellationToken);
            using var response = await httpClient.PostAsync(nextPageUri, content, cancellationToken);
            response.EnsureSuccessStatusCode();
            currentUri = response.RequestMessage!.RequestUri!;
            html = await response.Content.ReadAsStringAsync(cancellationToken);
        }
    }

    private static bool TryCreateNextPageRequest(
        IDocument document,
        Uri currentUri,
        out Uri nextPageUri,
        out Dictionary<string, string> formValues)
    {
        formValues = [];
        nextPageUri = currentUri;
        var pageStateInput = document.QuerySelector<IHtmlInputElement>("input[name='PRADO_PAGESTATE']");
        var pageState = pageStateInput?.Value;
        var currentPage = ParseInteger(document.QuerySelector("input[id$='numPageTop']")?.GetAttribute("value")) ?? 1;
        var pageCount = ParseInteger(document.QuerySelector("[id$='nombrePageTop']")?.TextContent) ?? 1;
        var nextPageLink = document.QuerySelector("a[id$='PagerTop_ctl2']");
        var nextPageId = nextPageLink?.GetAttribute("id");
        var nextPageTarget = nextPageId is null ? null : GetPradoEventTarget(document, nextPageId);
        if (string.IsNullOrWhiteSpace(pageState)
            || currentPage >= pageCount
            || string.IsNullOrWhiteSpace(nextPageTarget))
        {
            return false;
        }

        var form = nextPageLink!.Closest("form") ?? pageStateInput!.Closest("form");
        nextPageUri = form?.GetAttribute("action") is { Length: > 0 } action
            ? new Uri(currentUri, action)
            : currentUri;
        formValues["PRADO_PAGESTATE"] = pageState;
        formValues["PRADO_POSTBACK_TARGET"] = nextPageTarget;
        formValues["PRADO_POSTBACK_PARAMETER"] = string.Empty;
        return true;
    }

    private static string? GetPradoEventTarget(IDocument document, string controlId)
    {
        var pattern = $@"\{{'ID':""{Regex.Escape(controlId)}"",'EventTarget':""(?<target>[^""]+)""";
        foreach (var script in document.QuerySelectorAll("script"))
        {
            var match = Regex.Match(script.TextContent, pattern, RegexOptions.CultureInvariant);
            if (match.Success)
            {
                return match.Groups["target"].Value;
            }
        }

        return null;
    }

    private static bool TryCreatePageSizeRequest(
        IDocument document,
        Uri currentUri,
        int advertisedCount,
        out Uri requestUri,
        out Dictionary<string, string> formValues)
    {
        formValues = [];
        requestUri = currentUri;
        var pageSize = document.QuerySelector("select[id$='listePageSizeTop']");
        var selectedSize = pageSize?.QuerySelector("option[selected]")?.GetAttribute("value");
        var pageStateInput = document.QuerySelector<IHtmlInputElement>("input[name='PRADO_PAGESTATE']");
        if (advertisedCount <= 10
            || pageSize?.QuerySelector("option[value='20']") is null
            || selectedSize == "20"
            || string.IsNullOrWhiteSpace(pageStateInput?.Value))
        {
            return false;
        }

        var form = pageSize.Closest("form") ?? pageStateInput.Closest("form");
        requestUri = form?.GetAttribute("action") is { Length: > 0 } action
            ? new Uri(currentUri, action)
            : currentUri;
        formValues["PRADO_PAGESTATE"] = pageStateInput.Value;
        formValues["PRADO_POSTBACK_TARGET"] = "ctl0$CONTENU_PAGE$resultSearch$listePageSizeTop";
        formValues["PRADO_POSTBACK_PARAMETER"] = string.Empty;
        formValues["ctl0$CONTENU_PAGE$resultSearch$listePageSizeTop"] = "20";
        return true;
    }

    private static int? ParseInteger(string? value)
    {
        var digits = value is null ? string.Empty : new string(value.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out var parsed) ? parsed : null;
    }

    private Task DelayAsync(CancellationToken cancellationToken) =>
        options.Value.RequestDelayMilliseconds == 0
            ? Task.CompletedTask
            : Task.Delay(options.Value.RequestDelayMilliseconds, cancellationToken);
}
