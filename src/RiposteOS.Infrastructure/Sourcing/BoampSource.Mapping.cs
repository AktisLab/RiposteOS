using System.Globalization;
using System.Text.Json;

namespace RiposteOS.Infrastructure.Sourcing;

public sealed partial class BoampSource
{
    private static readonly string[] DeadlinePeriodNames =
        ["cac:TenderSubmissionDeadlinePeriod", "cac:ParticipationRequestReceptionPeriod"];

    private static SourceOpportunity MapOpportunity(JsonElement record)
    {
        using var data = GetString(record, "donnees") is { } rawData
            ? JsonDocument.Parse(rawData)
            : null;
        var root = data?.RootElement;
        var estimatedAmount = root is { } value ? GetEstimatedAmount(value) : null;
        var sourceId = GetRequiredString(record, "idweb");
        var title = GetRequiredString(record, "objet");
        var publicationDate = DateOnly.Parse(
            GetRequiredString(record, "dateparution"),
            CultureInfo.InvariantCulture);
        var responseDeadline = GetResponseDeadline(record, root)
            ?? throw new FormatException("BOAMP response deadline is missing.");

        return new SourceOpportunity(
            sourceId,
            title,
            GetString(record, "nomacheteur") ?? "Acheteur non renseigné",
            publicationDate,
            responseDeadline,
            [FranceCountryCode],
            GetStrings(record, "code_departement_prestation", "code_departement"),
            root is { } cpvRoot ? GetCpvCodes(cpvRoot) : [],
            GetStrings(record, "descripteur_code"),
            GetStrings(record, "descripteur_libelle"),
            GetString(record, "url_avis") ?? string.Empty,
            record.GetRawText(),
            Description: root is { } descriptionRoot ? GetDescription(descriptionRoot) : null,
            ProcedureType: root is { } procedureRoot
                ? FindCodeListValue(procedureRoot, "procurement-procedure-type")
                    ?? GetString(record, "procedure_libelle")
                : GetString(record, "procedure_libelle"),
            ContractNature: root is { } natureRoot
                ? FindCodeListValue(natureRoot, "contract-nature")
                    ?? GetString(record, "nature_libelle")
                : GetString(record, "nature_libelle"),
            EstimatedValue: GetDecimalText(estimatedAmount),
            Currency: estimatedAmount is { } amount
                ? GetAttribute(amount, "@currencyID") ?? GetAttribute(amount, "@devise")
                : null,
            ExecutionDuration: root is { } durationRoot ? GetDuration(durationRoot) : null,
            DocumentUrl: root is { } documentRoot ? GetDocumentUrl(documentRoot) : null,
            EformsNoticeId: root is { } noticeRoot ? GetEformsNoticeId(noticeRoot) : null);
    }

    private static Guid? GetEformsNoticeId(JsonElement root)
    {
        var value = FindProperties(root, "cbc:ID")
            .Where(element => string.Equals(
                GetAttribute(element, "@schemeName"),
                "notice-id",
                StringComparison.OrdinalIgnoreCase))
            .Select(element => GetText(element))
            .FirstOrDefault(text => !string.IsNullOrWhiteSpace(text));
        if (value is null)
        {
            return null;
        }

        return Guid.TryParse(value, out var identifier)
            ? identifier
            : throw new FormatException("BOAMP eForms notice identifier is invalid.");
    }

    private static string GetRequiredString(JsonElement record, string propertyName) =>
        GetString(record, propertyName)
        ?? throw new JsonException($"BOAMP field '{propertyName}' is missing.");

    private static string? GetString(JsonElement record, string propertyName) =>
        record.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static DateTimeOffset? GetResponseDeadline(JsonElement record, JsonElement? root)
    {
        if (ParseFrenchDateTime(GetString(record, "datelimitereponse")) is { } explicitDeadline)
        {
            return explicitDeadline;
        }

        if (root is not { } value)
        {
            return null;
        }

        if (string.Equals(GetString(record, "nature_libelle"), "Rectificatif", StringComparison.OrdinalIgnoreCase))
        {
            return FindProperties(value, "lireDate")
                .Select(element => GetText(element))
                .Select(ParseFrenchDateTime)
                .Where(deadline => deadline is not null)
                .Min();
        }

        return DeadlinePeriodNames
            .SelectMany(propertyName => FindProperties(value, propertyName))
            .Select(ParsePeriodDeadline)
            .Where(deadline => deadline is not null)
            .Min();
    }

    private static DateTimeOffset? ParsePeriodDeadline(JsonElement period)
    {
        var date = GetText(FindFirstProperty(period, "cbc:EndDate"));
        if (date is null || date.Length < 10)
        {
            return null;
        }

        var time = GetText(FindFirstProperty(period, "cbc:EndTime"));
        var offsetSource = time ?? date;
        var offset = GetOffset(offsetSource);
        var localTime = time is { Length: >= 8 } ? time[..8] : "23:59:59";
        return ParseFrenchDateTime($"{date[..10]}T{localTime}{offset}");
    }

    private static DateTimeOffset? ParseDateTimeOffset(string? value) =>
        DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out var parsed)
            ? parsed.ToUniversalTime()
            : null;

    private static DateTimeOffset? ParseFrenchDateTime(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (value.EndsWith('Z') || GetOffset(value).Length > 0)
        {
            return ParseDateTimeOffset(value);
        }

        if (!DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var local))
        {
            return null;
        }

        local = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Paris");
        return new DateTimeOffset(local, timeZone.GetUtcOffset(local)).ToUniversalTime();
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
        return minus >= 8 ? value[minus..] : string.Empty;
    }

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

    private static string[] GetCpvCodes(JsonElement root)
    {
        var codes = new HashSet<string>(StringComparer.Ordinal);
        CollectCpvCodes(root, codes);
        return [.. codes];
    }

    private static string? GetScopedText(
        JsonElement root,
        string containerName,
        string propertyName)
    {
        var container = FindFirstProperty(root, containerName);
        return container is { } value
            ? GetText(FindFirstProperty(value, propertyName))
            : null;
    }

    private static string? GetDescription(JsonElement root) =>
        GetScopedText(root, "cac:ProcurementProject", "cbc:Description")
        ?? GetText(FindPath(root, "FNSimple", "initial", "natureMarche", "description"))
        ?? GetText(FindPath(root, "FNSimple", "rectificatif", "natureMarche", "description"))
        ?? GetText(FindPath(root, "FNSimple", "attribution", "natureMarche", "description"))
        ?? GetText(FindPath(root, "MAPA", "initial", "description", "objet"))
        ?? GetText(FindPath(root, "MAPA", "rectificatif", "description", "objet"))
        ?? GetText(FindPath(root, "MAPA", "attribution", "descriptionReduite", "objet"));

    private static JsonElement? GetEstimatedAmount(JsonElement root) =>
        FindPath(
            root,
            "EFORMS",
            "ContractNotice",
            "cac:ProcurementProject",
            "cac:RequestedTenderTotal",
            "cbc:EstimatedOverallContractAmount")
        ?? FindPath(root, "FNSimple", "initial", "natureMarche", "valeurEstimee")
        ?? FindPath(root, "FNSimple", "rectificatif", "natureMarche", "valeurEstimee");

    private static string? GetDocumentUrl(JsonElement root) =>
        GetScopedText(root, "cac:CallForTendersDocumentReference", "cbc:URI")
        ?? GetText(FindPath(root, "FNSimple", "initial", "communication", "urlDocConsul"))
        ?? GetText(FindPath(root, "FNSimple", "rectificatif", "communication", "urlDocConsul"))
        ?? GetText(FindPath(root, "FNSimple", "initial", "communication", "urlProfilAch"))
        ?? GetText(FindPath(root, "FNSimple", "rectificatif", "communication", "urlProfilAch"))
        ?? GetText(FindFirstProperty(root, "urlProfilAcheteur"));

    private static JsonElement? FindPath(JsonElement root, params string[] propertyNames)
    {
        var current = root;
        foreach (var propertyName in propertyNames)
        {
            if (current.ValueKind != JsonValueKind.Object
                || !current.TryGetProperty(propertyName, out current))
            {
                return null;
            }
        }

        return current;
    }

    private static string? FindCodeListValue(JsonElement root, string listName)
    {
        if (root.ValueKind == JsonValueKind.Object)
        {
            if (GetAttribute(root, "@listName") is { } name
                && string.Equals(name, listName, StringComparison.OrdinalIgnoreCase))
            {
                return GetText(root);
            }

            foreach (var property in root.EnumerateObject())
            {
                if (FindCodeListValue(property.Value, listName) is { } value)
                {
                    return value;
                }
            }
        }
        else if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in root.EnumerateArray())
            {
                if (FindCodeListValue(item, listName) is { } value)
                {
                    return value;
                }
            }
        }

        return null;
    }

    private static JsonElement? FindFirstProperty(JsonElement root, string propertyName)
    {
        if (root.ValueKind == JsonValueKind.Object)
        {
            if (root.TryGetProperty(propertyName, out var value))
            {
                return value;
            }

            foreach (var property in root.EnumerateObject())
            {
                if (FindFirstProperty(property.Value, propertyName) is { } nested)
                {
                    return nested;
                }
            }
        }
        else if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in root.EnumerateArray())
            {
                if (FindFirstProperty(item, propertyName) is { } nested)
                {
                    return nested;
                }
            }
        }

        return null;
    }

    private static IEnumerable<JsonElement> FindProperties(JsonElement root, string propertyName)
    {
        if (root.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in root.EnumerateObject())
            {
                if (string.Equals(property.Name, propertyName, StringComparison.Ordinal))
                {
                    yield return property.Value;
                }

                foreach (var nested in FindProperties(property.Value, propertyName))
                {
                    yield return nested;
                }
            }
        }
        else if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in root.EnumerateArray())
            {
                foreach (var nested in FindProperties(item, propertyName))
                {
                    yield return nested;
                }
            }
        }
    }

    private static string? GetText(JsonElement? element)
    {
        if (element is not { } value)
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.String)
        {
            return value.GetString();
        }

        return value.ValueKind == JsonValueKind.Object
            && value.TryGetProperty("#text", out var text)
            && text.ValueKind == JsonValueKind.String
                ? text.GetString()
                : null;
    }

    private static string? GetAttribute(JsonElement element, string attributeName) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(attributeName, out var attribute)
        && attribute.ValueKind == JsonValueKind.String
            ? attribute.GetString()
            : null;

    private static decimal? GetDecimalText(JsonElement? element) =>
        decimal.TryParse(
            element is { } value && value.ValueKind == JsonValueKind.Object
                ? GetText(FindPath(value, "valeur")) ?? GetText(value)
                : GetText(element),
            NumberStyles.Number,
            CultureInfo.InvariantCulture,
            out var amount)
                ? amount
                : null;

    private static string? GetDuration(JsonElement root)
    {
        var lot = FindFirstProperty(root, "cac:ProcurementProjectLot");
        var singleLot = lot is { ValueKind: JsonValueKind.Array } lots
            ? lots.GetArrayLength() == 1 ? lots[0] : (JsonElement?)null
            : lot;
        var plannedPeriod = singleLot is { } lotValue
            ? FindFirstProperty(lotValue, "cac:PlannedPeriod")
            : null;
        var duration = plannedPeriod is { } period
            ? FindFirstProperty(period, "cbc:DurationMeasure")
            : null;
        if (duration is { } measure && GetText(duration) is { } value)
        {
            var unit = GetAttribute(measure, "@unitCode");
            return unit is null ? value : $"{value} {unit}";
        }

        var months = GetText(FindPath(root, "FNSimple", "initial", "natureMarche", "dureeMois"))
            ?? GetText(FindPath(root, "FNSimple", "rectificatif", "natureMarche", "dureeMois"))
            ?? GetText(FindPath(root, "MAPA", "initial", "duree", "nbMois"))
            ?? GetText(FindPath(root, "MAPA", "rectificatif", "duree", "nbMois"));
        if (months is not null)
        {
            return $"{months} MONTH";
        }

        return GetText(FindPath(root, "MAPA", "initial", "duree", "txtLibre"))
            ?? GetText(FindPath(root, "MAPA", "rectificatif", "duree", "txtLibre"));
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
