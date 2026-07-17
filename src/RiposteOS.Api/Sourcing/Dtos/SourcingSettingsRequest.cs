namespace RiposteOS.Api.Sourcing.Dtos;

public sealed record SourcingSettingsRequest(
    string[]? Keywords,
    string[]? ExcludedKeywords,
    string[]? PositiveSignals,
    string[]? NegativeSignals,
    string[]? AllowedCountryCodes,
    string[]? PreferredDepartmentCodes,
    string[]? CpvWhitelistPrefixes,
    string[]? CpvWatchPrefixes,
    string[]? CpvExcludedPrefixes,
    int PageSize,
    int PositiveSignalWeight,
    int NegativeSignalPenalty,
    int PreferredDepartmentBoost,
    int CpvWhitelistBoost,
    int CpvWatchBoost,
    int CpvExclusionPenalty,
    int UrgentDeadlineDays,
    int UrgentDeadlinePenalty,
    int HighRelevanceThreshold,
    string? BoampCron = null,
    string? TedCron = null);
