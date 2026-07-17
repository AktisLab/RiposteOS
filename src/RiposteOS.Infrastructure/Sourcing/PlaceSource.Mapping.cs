using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AngleSharp.Dom;

namespace RiposteOS.Infrastructure.Sourcing;

public sealed partial class PlaceSource
{
    private static readonly Regex DepartmentCodePattern = new(
        @"\b(?:2A|2B|\d{2,3})\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex BoampReferencePattern = new(
        @"\b\d{2}-\d{4,}\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex TedReferencePattern = new(
        @"\b\d{6}-\d{4}\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly IReadOnlyDictionary<string, int> FrenchMonths =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["janv"] = 1,
            ["janvier"] = 1,
            ["fev"] = 2,
            ["fevr"] = 2,
            ["fevrier"] = 2,
            ["mars"] = 3,
            ["avr"] = 4,
            ["avril"] = 4,
            ["mai"] = 5,
            ["juin"] = 6,
            ["juil"] = 7,
            ["juillet"] = 7,
            ["aout"] = 8,
            ["sept"] = 9,
            ["septembre"] = 9,
            ["oct"] = 10,
            ["octobre"] = 10,
            ["nov"] = 11,
            ["novembre"] = 11,
            ["dec"] = 12,
            ["decembre"] = 12,
        };

    private static IEnumerable<PlaceSearchItem> ParseSearchItems(IDocument document, Uri currentUri)
    {
        foreach (var element in document.QuerySelectorAll(".item_consultation"))
        {
            var consultationId = element.QuerySelector("input[name$='$refCons']")?.GetAttribute("value")?.Trim();
            var organizationCode = element.QuerySelector("input[name$='$orgCons']")?.GetAttribute("value")?.Trim();
            if (string.IsNullOrWhiteSpace(consultationId) || string.IsNullOrWhiteSpace(organizationCode))
            {
                throw new FormatException("PLACE result identifiers are missing.");
            }

            var sourceId = $"{consultationId}:{organizationCode}";

            var title = GetAttributeOrText(
                element,
                ".objet-line .truncate [title]",
                ".objet-line [title]",
                ".objet-line")
                ?? throw new FormatException($"PLACE result '{sourceId}' has no title.");
            var buyer = GetAttributeOrText(
                element,
                ".panelBlocDenomination [title]",
                ".denomination-line [title]",
                ".denomination-line")
                ?? "Acheteur non renseigné";
            var description = GetAttributeOrText(
                element,
                ".panelBlocObjet [title]",
                ".panelBlocObjet",
                ".objet-line .description");
            var procedureType = GetAttributeOrText(element, ".cons_procedure abbr[title]", ".cons_procedure");
            var contractNature = GetAttributeOrText(element, ".cons_categorie abbr[title]", ".cons_categorie");
            var publicationDate = ParsePublicationDate(element);
            var departments = DepartmentCodePattern.Matches(
                    element.QuerySelector(".lieux-exe")?.TextContent ?? string.Empty)
                .Select(match => match.Value.ToUpperInvariant())
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            var noticeUrl = new Uri(
                currentUri,
                $"/app.php/entreprise/consultation/{consultationId}?orgAcronyme={Uri.EscapeDataString(organizationCode)}")
                .AbsoluteUri;

            yield return new PlaceSearchItem(
                sourceId,
                organizationCode,
                title,
                buyer,
                publicationDate,
                description,
                procedureType,
                contractNature,
                departments,
                noticeUrl);
        }
    }

    private static SourceOpportunity MapOpportunity(PlaceSearchItem item, string detailHtml)
    {
        var document = new AngleSharp.Html.Parser.HtmlParser().ParseDocument(detailHtml);
        var buyer = GetDetailValue(document, "Entité d'achat", "Entité d’achat", "Organisme") ?? item.Buyer;
        var description = GetDetailValue(document, "Objet", "Description") ?? item.Description;
        var procedureType = GetDetailValue(document, "Procédure", "Type de procédure") ?? item.ProcedureType;
        var contractNature = GetDetailValue(document, "Catégorie", "Nature du marché") ?? item.ContractNature;
        var deadline = ParseDeadline(GetDetailValue(document, "Date limite de remise des plis", "Date limite"));
        var cpvCodes = document.QuerySelectorAll("[data-code-cpv]")
            .Select(element => element.GetAttribute("data-code-cpv")!)
            .Select(NormalizeCpvCode)
            .Where(code => code is not null)
            .Select(code => code!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var documentUrl = GetAbsoluteUrl(
            item.NoticeUrl,
            document.QuerySelector("#linkDownloadDce, a[href*='EntrepriseDemandeTelechargementDce']")
                ?.GetAttribute("href"));
        var detailDepartments = DepartmentCodePattern.Matches(
                GetDetailValue(document, "Lieu d'exécution", "Lieux d'exécution", "Département")
                ?? string.Empty)
            .Select(match => match.Value.ToUpperInvariant());
        var references = GetReferences(document);
        var snapshot = new PlaceSnapshot(
            item.SourceId,
            item.Title,
            buyer,
            item.PublicationDate,
            deadline,
            item.DepartmentCodes.Concat(detailDepartments)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray(),
            cpvCodes.Order(StringComparer.Ordinal).ToArray(),
            item.NoticeUrl,
            description,
            procedureType,
            contractNature,
            documentUrl,
            references.OrderBy(reference => reference.Source, StringComparer.Ordinal)
                .ThenBy(reference => reference.SourceId, StringComparer.Ordinal)
                .ToArray());
        var rawPayload = JsonSerializer.Serialize(snapshot, SerializerOptions);
        return ToSourceOpportunity(snapshot, rawPayload);
    }

    private static SourceOpportunity ToSourceOpportunity(PlaceSnapshot snapshot, string rawPayload) =>
        new(
            snapshot.SourceId,
            snapshot.Title,
            snapshot.Buyer,
            snapshot.PublicationDate,
            snapshot.ResponseDeadline,
            [FranceCountryCode],
            snapshot.DepartmentCodes,
            snapshot.CpvCodes,
            [],
            [],
            snapshot.NoticeUrl,
            rawPayload,
            Description: snapshot.Description,
            ProcedureType: snapshot.ProcedureType,
            ContractNature: snapshot.ContractNature,
            DocumentUrl: snapshot.DocumentUrl)
        {
            References = snapshot.References,
        };

    private static DateOnly ParsePublicationDate(IElement element)
    {
        var machineDate = element.QuerySelector("time[datetime]")?.GetAttribute("datetime");
        if (machineDate is { Length: >= 10 }
            && DateOnly.TryParse(machineDate[..10], CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            return parsed;
        }

        var day = element.QuerySelector(".date .day")?.TextContent.Trim();
        var month = NormalizeText(element.QuerySelector(".date .month")?.TextContent ?? string.Empty);
        var year = element.QuerySelector(".date .year")?.TextContent.Trim();
        var monthNumber = FrenchMonths.GetValueOrDefault(month);
        if (int.TryParse(day, CultureInfo.InvariantCulture, out var dayNumber)
            && int.TryParse(year, CultureInfo.InvariantCulture, out var yearNumber)
            && monthNumber > 0)
        {
            return new DateOnly(yearNumber, monthNumber, dayNumber);
        }

        throw new FormatException("PLACE publication date is invalid.");
    }

    private static DateTimeOffset? ParseDeadline(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var formats = new[] { "dd/MM/yyyy HH:mm", "dd/MM/yyyy H:mm" };
        if (!DateTime.TryParseExact(
                value.Trim(),
                formats,
                CultureInfo.GetCultureInfo("fr-FR"),
                DateTimeStyles.AllowWhiteSpaces,
                out var local))
        {
            return null;
        }

        local = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Paris");
        return new DateTimeOffset(local, timeZone.GetUtcOffset(local)).ToUniversalTime();
    }

    private static SourceOpportunityReference[] GetReferences(IDocument document)
    {
        var references = new HashSet<SourceOpportunityReference>();
        foreach (var link in document.QuerySelectorAll("a[href]"))
        {
            var href = link.GetAttribute("href")!;
            var content = $"{href} {link.TextContent}";
            if (content.Contains("boamp", StringComparison.OrdinalIgnoreCase)
                && BoampReferencePattern.Match(content) is { Success: true } boamp)
            {
                references.Add(new SourceOpportunityReference("BOAMP", boamp.Value));
            }

            if (content.Contains("ted", StringComparison.OrdinalIgnoreCase)
                && TedReferencePattern.Match(content) is { Success: true } ted)
            {
                references.Add(new SourceOpportunityReference("TED", ted.Value));
            }
        }

        return [.. references];
    }

    private static string? GetDetailValue(IDocument document, params string[] labels)
    {
        foreach (var label in document.QuerySelectorAll("label, dt, th, strong, .label"))
        {
            if (!labels.Any(candidate => NormalizeText(label.TextContent)
                    .StartsWith(NormalizeText(candidate), StringComparison.Ordinal)))
            {
                continue;
            }

            var value = label.ParentElement!.QuerySelector(".green.bold, .value, dd, td, span:not(.label)")
                ?.TextContent.Trim();
            if (!string.IsNullOrWhiteSpace(value) && !string.Equals(value, label.TextContent.Trim(), StringComparison.Ordinal))
            {
                return value;
            }

            var sibling = label.NextElementSibling?.TextContent.Trim();
            if (!string.IsNullOrWhiteSpace(sibling))
            {
                return sibling;
            }
        }

        return null;
    }

    private static string? GetAttributeOrText(IElement parent, params string[] selectors)
    {
        foreach (var selector in selectors)
        {
            var element = parent.QuerySelector(selector);
            var value = element?.GetAttribute("title") ?? element?.TextContent;
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }

    private static string? NormalizeCpvCode(string value)
    {
        var digits = new string(value.Where(char.IsDigit).ToArray());
        return digits.Length >= 8 ? digits[..8] : null;
    }

    private static string? GetAbsoluteUrl(string baseUrl, string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : new Uri(new Uri(baseUrl), value).AbsoluteUri;

    private static string NormalizeText(string value)
    {
        var result = new StringBuilder(value.Length);
        foreach (var character in value.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                result.Append(char.ToLowerInvariant(character));
            }
        }

        return string.Join(' ', result.ToString()
            .Replace(':', ' ')
            .Replace('.', ' ')
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}
