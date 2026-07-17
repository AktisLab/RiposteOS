using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RiposteOS.Core.Sourcing;

namespace RiposteOS.Infrastructure.Sourcing;

public sealed partial class BoampSource(
    HttpClient httpClient,
    IOptions<BoampOptions> options) : IOpportunitySource
{
    private const string FranceCountryCode = "FRA";

    public string Key => SourcingSource.Boamp;

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

        var cpvPrefixes = settings.CpvWhitelistPrefixes
            .Concat(settings.CpvWatchPrefixes)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        for (var publicationDate = startDate; publicationDate <= endDate; publicationDate = publicationDate.AddDays(1))
        {
            var offset = 0;

            while (true)
            {
                var page = await SearchPageAsync(
                    settings.Keywords,
                    settings.ExcludedKeywords,
                    cpvPrefixes,
                    publicationDate,
                    offset,
                    settings.PageSize,
                    cancellationToken);
                yield return new SourcingPage(
                    page.PublicationDate,
                    page.Fetched,
                    page.Opportunities,
                    page.Skipped,
                    page.Issues);

                offset += page.Fetched;
                if (page.Fetched == 0 || offset >= page.TotalCount)
                {
                    break;
                }
            }
        }
    }

    public async Task<BoampPage> SearchPageAsync(
        IReadOnlyCollection<string> keywords,
        IReadOnlyCollection<string> excludedKeywords,
        IReadOnlyCollection<string> cpvPrefixes,
        DateOnly publicationDate,
        int offset,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var included = keywords
            .Where(keyword => !string.IsNullOrWhiteSpace(keyword))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var includedCpvPrefixes = cpvPrefixes
            .Where(prefix => !string.IsNullOrWhiteSpace(prefix))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (included.Length == 0 && includedCpvPrefixes.Length == 0)
        {
            return new BoampPage(publicationDate, 0, 0, [], 0);
        }

        var collectionQuery = string.Join(
            " or ",
            included.Select(keyword => $"search(objet, \"{EscapeQueryValue(keyword)}\")")
                .Concat(includedCpvPrefixes.Select(prefix =>
                    $"search(donnees, \"CPV {EscapeQueryValue(prefix)}\")")));
        var excludedQuery = string.Join(
            " or ",
            excludedKeywords
                .Where(keyword => !string.IsNullOrWhiteSpace(keyword))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(keyword => $"search(objet, \"{EscapeQueryValue(keyword)}\")"));
        var keywordQuery = excludedQuery.Length == 0
            ? collectionQuery
            : $"({collectionQuery}) and not ({excludedQuery})";
        var where = $"dateparution = date'{publicationDate:yyyy-MM-dd}' and ({keywordQuery})";
        var requestUri =
            $"records?where={Uri.EscapeDataString(where)}&order_by={Uri.EscapeDataString("idweb asc")}&limit={Math.Clamp(pageSize, 1, 100).ToString(CultureInfo.InvariantCulture)}&offset={Math.Max(offset, 0).ToString(CultureInfo.InvariantCulture)}";

        using var response = await httpClient.GetAsync(requestUri, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var results = document.RootElement.GetProperty("results");
        var opportunities = new List<SourceOpportunity>();
        var issues = new List<SourceImportIssue>();
        var skipped = 0;

        foreach (var record in results.EnumerateArray())
        {
            try
            {
                opportunities.Add(MapOpportunity(record));
            }
            catch (JsonException)
            {
                skipped++;
                issues.Add(CreateIssue(record, "mapping_json"));
            }
            catch (FormatException)
            {
                skipped++;
                issues.Add(CreateIssue(record, "mapping_format"));
            }
        }

        return new BoampPage(
            publicationDate,
            results.GetArrayLength(),
            document.RootElement.GetProperty("total_count").GetInt32(),
            opportunities,
            skipped,
            issues);
    }

    public SourceOpportunity ParseRawOpportunity(string rawPayload)
    {
        using var document = JsonDocument.Parse(rawPayload);
        return MapOpportunity(document.RootElement);
    }

    private static SourceImportIssue CreateIssue(JsonElement record, string errorCode) =>
        new(GetString(record, "idweb"), errorCode, record.GetRawText());

    private static string EscapeQueryValue(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
}
