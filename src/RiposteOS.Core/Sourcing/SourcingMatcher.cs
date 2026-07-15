namespace RiposteOS.Core.Sourcing;

public static class SourcingMatcher
{
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
        var searchableText = string.Join(' ', descriptorLabels.Prepend(title));

        foreach (var signal in settings.PositiveSignals.Where(signal =>
                     searchableText.Contains(signal, StringComparison.OrdinalIgnoreCase)))
        {
            score += settings.PositiveSignalWeight;
            reasons.Add($"+{settings.PositiveSignalWeight} Signal positif : {signal}");
        }

        foreach (var signal in settings.NegativeSignals.Where(signal =>
                     searchableText.Contains(signal, StringComparison.OrdinalIgnoreCase)))
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
}
