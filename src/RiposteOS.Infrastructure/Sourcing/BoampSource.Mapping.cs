using System.Globalization;
using System.Text.Json;

namespace RiposteOS.Infrastructure.Sourcing;

public sealed partial class BoampSource
{
    private static SourceOpportunity MapOpportunity(JsonElement record) => new(
        GetRequiredString(record, "idweb"),
        GetRequiredString(record, "objet"),
        GetString(record, "nomacheteur") ?? "Acheteur non renseigné",
        DateOnly.Parse(GetRequiredString(record, "dateparution"), CultureInfo.InvariantCulture),
        GetDateTimeOffset(record, "datelimitereponse"),
        GetStrings(record, "code_departement_prestation", "code_departement"),
        GetCpvCodes(record),
        GetStrings(record, "descripteur_code"),
        GetStrings(record, "descripteur_libelle"),
        GetString(record, "url_avis") ?? string.Empty,
        record.GetRawText());

    private static string GetRequiredString(JsonElement record, string propertyName) =>
        GetString(record, propertyName)
        ?? throw new JsonException($"BOAMP field '{propertyName}' is missing.");

    private static string? GetString(JsonElement record, string propertyName) =>
        record.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static DateTimeOffset? GetDateTimeOffset(JsonElement record, string propertyName) =>
        DateTimeOffset.TryParse(
            GetString(record, propertyName),
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out var value)
            ? value
            : null;

    private static string[] GetStrings(JsonElement record, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (!record.TryGetProperty(propertyName, out var value) || value.ValueKind == JsonValueKind.Null)
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.Array)
            {
                return value.EnumerateArray()
                    .Where(item => item.ValueKind == JsonValueKind.String)
                    .Select(item => item.GetString()!)
                    .ToArray();
            }

            if (value.ValueKind == JsonValueKind.String && value.GetString() is { } singleValue)
            {
                return [singleValue];
            }
        }

        return [];
    }

    private static string[] GetCpvCodes(JsonElement record)
    {
        if (GetString(record, "donnees") is not { } data)
        {
            return [];
        }

        using var document = JsonDocument.Parse(data);
        var codes = new HashSet<string>(StringComparer.Ordinal);
        CollectCpvCodes(document.RootElement, codes);
        return [.. codes];
    }

    private static void CollectCpvCodes(JsonElement element, HashSet<string> codes)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty("@listName", out var listName)
                && string.Equals(listName.GetString(), "cpv", StringComparison.OrdinalIgnoreCase)
                && element.TryGetProperty("#text", out var code))
            {
                AddCpvCode(code, codes);
            }

            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, "CPV", StringComparison.OrdinalIgnoreCase))
                {
                    CollectEightDigitCodes(property.Value, codes);
                }

                CollectCpvCodes(property.Value, codes);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                CollectCpvCodes(item, codes);
            }
        }
    }

    private static void CollectEightDigitCodes(JsonElement element, HashSet<string> codes)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            AddCpvCode(element, codes);
            return;
        }

        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                CollectEightDigitCodes(property.Value, codes);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                CollectEightDigitCodes(item, codes);
            }
        }
    }

    private static void AddCpvCode(JsonElement element, HashSet<string> codes)
    {
        if (element.ValueKind == JsonValueKind.String
            && element.GetString() is { Length: 8 } value
            && value.All(char.IsDigit))
        {
            codes.Add(value);
        }
    }
}
