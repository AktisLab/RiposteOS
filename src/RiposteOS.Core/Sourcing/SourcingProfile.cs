namespace RiposteOS.Core.Sourcing;

public sealed record SourcingProfile(
    IReadOnlyCollection<string> Keywords,
    IReadOnlyCollection<string> ExcludedKeywords,
    IReadOnlyCollection<string> PositiveSignals,
    IReadOnlyCollection<string> NegativeSignals,
    IReadOnlyCollection<string> PreferredDepartmentCodes,
    IReadOnlyCollection<string> CpvWhitelistPrefixes,
    IReadOnlyCollection<string> CpvWatchPrefixes,
    IReadOnlyCollection<string> CpvExcludedPrefixes,
    int PageSize,
    int PositiveSignalWeight,
    int NegativeSignalPenalty,
    int PreferredDepartmentBoost,
    int CpvWhitelistBoost,
    int CpvWatchBoost,
    int CpvExclusionPenalty,
    int UrgentDeadlineDays,
    int UrgentDeadlinePenalty,
    int HighRelevanceThreshold);
