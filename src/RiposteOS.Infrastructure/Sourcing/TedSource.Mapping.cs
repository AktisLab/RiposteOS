using System.Globalization;
using System.Text.Json;

namespace RiposteOS.Infrastructure.Sourcing;

public sealed partial class TedSource
{
    private static SourceOpportunity MapOpportunity(JsonElement notice) => new(
        GetRequiredString(notice, "publication-number"),
        GetRequiredLocalizedString(notice, "notice-title"),
        GetLocalizedString(notice, "buyer-name") ?? "Acheteur non renseigné",
        GetPublicationDate(notice),
        GetResponseDeadline(notice),
        GetStrings(notice, "place-of-performance-country-lot")
            .Select(country => country.ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToArray(),
        GetDepartmentCodes(notice),
        GetStrings(notice, "classification-cpv").Distinct(StringComparer.Ordinal).ToArray(),
        [],
        [],
        GetNoticeUrl(notice),
        notice.GetRawText(),
        Description: GetLocalizedString(notice, "description-proc")
            ?? GetSingleLocalizedString(notice, "description-lot"),
        ProcedureType: GetStrings(notice, "procedure-type").FirstOrDefault(),
        ContractNature: GetStrings(notice, "contract-nature").FirstOrDefault(),
        EstimatedValue: GetEstimatedValue(notice),
        Currency: GetEstimatedCurrency(notice),
        ExecutionDuration: GetExecutionDuration(notice),
        DocumentUrl: GetStrings(notice, "document-url-lot").FirstOrDefault()
            ?? GetStrings(notice, "document-restricted-url-lot").FirstOrDefault());

    private static decimal? GetEstimatedValue(JsonElement notice)
    {
        var value = GetStrings(notice, "estimated-value-proc").FirstOrDefault();
        if (value is null)
        {
            var lotValues = GetStrings(notice, "estimated-value-lot");
            value = lotValues.Length == 1 ? lotValues[0] : null;
        }

        return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount)
            ? amount
            : null;
    }

    private static string? GetEstimatedCurrency(JsonElement notice)
    {
        if (GetStrings(notice, "estimated-value-cur-proc").FirstOrDefault() is { } procedureCurrency)
        {
            return procedureCurrency;
        }

        var lotCurrencies = GetStrings(notice, "estimated-value-cur-lot");
        return lotCurrencies.Length == 1 ? lotCurrencies[0] : null;
    }

    private static string? GetExecutionDuration(JsonElement notice)
    {
        var values = GetStrings(notice, "duration-period-value-lot");
        if (values.Length != 1)
        {
            return null;
        }

        var units = GetStrings(notice, "duration-period-unit-lot");
        var unit = units.Length == 1 ? units[0] : null;
        return unit is null ? values[0] : $"{values[0]} {unit}";
    }

    private static string GetRequiredString(JsonElement notice, string propertyName) =>
        GetString(notice, propertyName)
        ?? throw new JsonException($"TED field '{propertyName}' is missing.");

    private static string GetRequiredLocalizedString(JsonElement notice, string propertyName) =>
        GetLocalizedString(notice, propertyName)
        ?? throw new JsonException($"TED field '{propertyName}' is missing.");

    private static string? GetString(JsonElement notice, string propertyName) =>
        notice.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string? GetLocalizedString(JsonElement notice, string propertyName)
    {
        if (!notice.TryGetProperty(propertyName, out var translations)
            || translations.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var language in new[] { "fra", "eng" })
        {
            if (TryGetLocalizedValue(translations, language) is { } preferred)
            {
                return preferred;
            }
        }

        return translations.EnumerateObject()
            .Select(translation => GetFirstString(translation.Value))
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }

    private static string? GetSingleLocalizedString(JsonElement notice, string propertyName)
    {
        if (!notice.TryGetProperty(propertyName, out var translations)
            || translations.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var language in new[] { "fra", "eng" })
        {
            foreach (var translation in translations.EnumerateObject())
            {
                if (string.Equals(translation.Name, language, StringComparison.OrdinalIgnoreCase))
                {
                    var values = GetStrings(translation.Value);
                    return values.Length == 1 ? values[0] : null;
                }
            }
        }

        return translations.EnumerateObject()
            .Select(translation => GetStrings(translation.Value))
            .Where(values => values.Length > 0)
            .Select(values => values.Length == 1 ? values[0] : null)
            .FirstOrDefault();
    }

    private static string? TryGetLocalizedValue(JsonElement translations, string language)
    {
        foreach (var translation in translations.EnumerateObject())
        {
            if (string.Equals(translation.Name, language, StringComparison.OrdinalIgnoreCase))
            {
                return GetFirstString(translation.Value);
            }
        }

        return null;
    }

    private static string? GetFirstString(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String)
        {
            return value.GetString();
        }

        return value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString())
                .FirstOrDefault(item => !string.IsNullOrWhiteSpace(item))
            : null;
    }

    private static DateOnly GetPublicationDate(JsonElement notice)
    {
        var value = GetRequiredString(notice, "publication-date");
        if (value.Length >= 10
            && DateOnly.TryParseExact(
                value[..10],
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var publicationDate))
        {
            return publicationDate;
        }

        throw new FormatException("TED publication date is invalid.");
    }

    private static DateTimeOffset? GetResponseDeadline(JsonElement notice)
    {
        var dates = GetStrings(notice, "deadline-receipt-tender-date-lot");
        var times = GetStrings(notice, "deadline-receipt-tender-time-lot");
        var deadlines = new List<DateTimeOffset>();

        for (var index = 0; index < dates.Length; index++)
        {
            var date = dates[index];
            if (date.Length < 10)
            {
                continue;
            }

            var time = index < times.Length ? times[index] : null;
            var offset = time is null ? GetOffset(date) : GetOffset(time);
            var localTime = time is { Length: >= 8 } ? time[..8] : "23:59:59";
            if (DateTimeOffset.TryParse(
                $"{date[..10]}T{localTime}{offset}",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var deadline))
            {
                deadlines.Add(deadline);
            }
        }

        return deadlines.Count == 0 ? null : deadlines.Min().ToUniversalTime();
    }

    private static string GetOffset(string value)
    {
        if (value.EndsWith('Z'))
        {
            return "Z";
        }

        var plus = value.LastIndexOf('+');
        if (plus >= 0)
        {
            return value[plus..];
        }

        var minus = value.LastIndexOf('-');
        return minus >= 8 ? value[minus..] : "Z";
    }

    private static string[] GetDepartmentCodes(JsonElement notice)
    {
        var countries = GetStrings(notice, "place-of-performance-country-lot");
        var postcodes = GetStrings(notice, "place-of-performance-post-code-lot");
        if (!countries.Contains("FRA", StringComparer.OrdinalIgnoreCase))
        {
            return [];
        }

        var frenchPostcodes = countries.Length == postcodes.Length
            ? postcodes.Where((_, index) => string.Equals(
                countries[index],
                "FRA",
                StringComparison.OrdinalIgnoreCase))
            : countries.All(country => string.Equals(country, "FRA", StringComparison.OrdinalIgnoreCase))
                ? postcodes
                : [];

        return frenchPostcodes
            .Select(ToDepartmentCode)
            .Where(code => code is not null)
            .Select(code => code!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string? ToDepartmentCode(string postcode)
    {
        var digits = new string(postcode.Where(char.IsDigit).ToArray());
        if (digits.Length < 2)
        {
            return null;
        }

        return digits.StartsWith("97", StringComparison.Ordinal) || digits.StartsWith("98", StringComparison.Ordinal)
            ? digits.Length >= 3 ? digits[..3] : null
            : digits[..2];
    }

    private static string GetNoticeUrl(JsonElement notice)
    {
        if (!notice.TryGetProperty("links", out var links)
            || links.ValueKind != JsonValueKind.Object
            || !links.TryGetProperty("html", out var html)
            || html.ValueKind != JsonValueKind.Object)
        {
            return string.Empty;
        }

        return TryGetLocalizedValue(html, "fra")
            ?? TryGetLocalizedValue(html, "eng")
            ?? html.EnumerateObject()
                .Select(link => GetFirstString(link.Value))
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))
            ?? string.Empty;
    }

    private static string[] GetStrings(JsonElement notice, string propertyName)
    {
        if (!notice.TryGetProperty(propertyName, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return [];
        }

        if (value.ValueKind == JsonValueKind.String && value.GetString() is { } singleValue)
        {
            return [singleValue];
        }

        return value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString()!)
                .ToArray()
            : [];
    }

    private static string[] GetStrings(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String && value.GetString() is { } singleValue)
        {
            return [singleValue];
        }

        return value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString()!)
                .ToArray()
            : [];
    }
}
