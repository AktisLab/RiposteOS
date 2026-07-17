using System.Globalization;
using System.Text;

namespace RiposteOS.Core.Sourcing;

public static class SourcingMatcher
{
    private static readonly string[] AmbiguousPositiveSignals =
        ["api", "conception et realisation", "conception realisation"];

    private static readonly string[] DigitalContextSignals =
    [
        "application",
        "applicatif",
        "data",
        "donnees",
        "developpement",
        "digital",
        "extranet",
        "informatique",
        "intranet",
        "logiciel",
        "numerique",
        "plateforme",
        "portail",
        "rgaa",
        "site internet",
        "site web",
        "ux",
        "web",
        "workflow",
    ];

    public static SourcingMatch Evaluate(
        SourcingSettings settings,
        string title,
        IReadOnlyCollection<string> departmentCodes,
        IReadOnlyCollection<string> cpvCodes,
        IReadOnlyCollection<string> descriptorLabels,
        DateTimeOffset? responseDeadline,
        DateTimeOffset evaluatedAt)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(departmentCodes);
        ArgumentNullException.ThrowIfNull(cpvCodes);
        ArgumentNullException.ThrowIfNull(descriptorLabels);

        var reasons = new List<string>();
        var score = 0;
        var searchableText = NormalizeForMatching(string.Join(' ', descriptorLabels.Prepend(title)));
        var hasDigitalContext = DigitalContextSignals.Any(signal => ContainsSignal(searchableText, signal))
            || settings.PositiveSignals.Any(signal =>
                !IsAmbiguousPositiveSignal(signal) && ContainsSignal(searchableText, signal))
            || FindCpv(cpvCodes, settings.CpvWhitelistPrefixes) is not null
            || FindCpv(cpvCodes, settings.CpvWatchPrefixes) is not null;

        foreach (var signal in settings.PositiveSignals.Where(signal =>
                     ContainsSignal(searchableText, signal)
                     && (!IsAmbiguousPositiveSignal(signal)
                         || hasDigitalContext)))
        {
            score += settings.PositiveSignalWeight;
            reasons.Add($"+{settings.PositiveSignalWeight} Signal positif : {signal}");
        }

        foreach (var signal in settings.NegativeSignals.Where(signal =>
                     ContainsSignal(searchableText, signal)))
        {
            score -= settings.NegativeSignalPenalty;
            reasons.Add($"-{settings.NegativeSignalPenalty} Signal négatif : {signal}");
        }

        var whitelistedCpv = FindCpv(cpvCodes, settings.CpvWhitelistPrefixes);
        var watchedCpv = whitelistedCpv is null
            ? FindCpv(cpvCodes, settings.CpvWatchPrefixes)
            : null;
        var excludedCpv = FindCpv(cpvCodes, settings.CpvExcludedPrefixes);

        if (whitelistedCpv is not null)
        {
            score += settings.CpvWhitelistBoost;
            reasons.Add($"+{settings.CpvWhitelistBoost} CPV ciblé : {whitelistedCpv}");
        }
        else if (watchedCpv is not null)
        {
            score += settings.CpvWatchBoost;
            reasons.Add($"+{settings.CpvWatchBoost} CPV surveillé : {watchedCpv}");
        }

        if (excludedCpv is not null)
        {
            score -= settings.CpvExclusionPenalty;
            reasons.Add($"-{settings.CpvExclusionPenalty} CPV exclu : {excludedCpv}");
        }

        var preferredDepartment = departmentCodes.FirstOrDefault(code =>
            settings.PreferredDepartmentCodes.Contains(code, StringComparer.OrdinalIgnoreCase));
        if (preferredDepartment is not null)
        {
            score += settings.PreferredDepartmentBoost;
            reasons.Add($"+{settings.PreferredDepartmentBoost} Territoire prioritaire : {preferredDepartment}");
        }

        if (responseDeadline is { } deadline)
        {
            var remainingDays = (deadline - evaluatedAt).TotalDays;
            if (remainingDays < 0)
            {
                score -= settings.UrgentDeadlinePenalty;
                reasons.Add($"-{settings.UrgentDeadlinePenalty} Échéance dépassée");
            }
            else if (remainingDays <= settings.UrgentDeadlineDays)
            {
                score -= settings.UrgentDeadlinePenalty;
                reasons.Add($"-{settings.UrgentDeadlinePenalty} Échéance sous {settings.UrgentDeadlineDays} jours");
            }
        }

        return new SourcingMatch(Math.Clamp(score, 0, 100), [.. reasons]);
    }

    private static string? FindCpv(
        IEnumerable<string> cpvCodes,
        IEnumerable<string> prefixes) =>
        cpvCodes.FirstOrDefault(code => prefixes.Any(prefix =>
            code.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)));

    private static bool ContainsSignal(string normalizedText, string signal)
    {
        var normalizedSignal = NormalizeForMatching(signal);
        return normalizedSignal.Length > 0
            && $" {normalizedText} ".Contains($" {normalizedSignal} ", StringComparison.Ordinal);
    }

    private static bool IsAmbiguousPositiveSignal(string signal) =>
        AmbiguousPositiveSignals.Contains(NormalizeForMatching(signal), StringComparer.Ordinal);

    private static string NormalizeForMatching(string value)
    {
        var result = new StringBuilder(value.Length);
        var needsSeparator = false;

        foreach (var rune in value.Normalize(NormalizationForm.FormD).EnumerateRunes())
        {
            if (Rune.GetUnicodeCategory(rune) is UnicodeCategory.NonSpacingMark
                or UnicodeCategory.SpacingCombiningMark
                or UnicodeCategory.EnclosingMark)
            {
                continue;
            }

            if (Rune.IsLetterOrDigit(rune))
            {
                if (needsSeparator && result.Length > 0)
                {
                    result.Append(' ');
                }

                result.Append(Rune.ToLowerInvariant(rune));
                needsSeparator = false;
            }
            else
            {
                needsSeparator = true;
            }
        }

        return result.ToString();
    }
}
