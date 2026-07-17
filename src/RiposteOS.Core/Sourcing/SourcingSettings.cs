namespace RiposteOS.Core.Sourcing;

public sealed class SourcingSettings
{
    public const int DefaultId = 1;
    public const string DefaultSynchronizationCron = "0 * * * *";
    public const string DefaultPlaceSynchronizationCron = "0 6,18 * * *";

    private string[] _keywords = [];
    private string[] _excludedKeywords = [];
    private string[] _positiveSignals = [];
    private string[] _negativeSignals = [];
    private string[] _allowedCountryCodes = [];
    private string[] _preferredDepartmentCodes = [];
    private string[] _cpvWhitelistPrefixes = [];
    private string[] _cpvWatchPrefixes = [];
    private string[] _cpvExcludedPrefixes = [];

    private SourcingSettings(
        int id,
        int pageSize,
        int positiveSignalWeight,
        int negativeSignalPenalty,
        int preferredDepartmentBoost,
        int cpvWhitelistBoost,
        int cpvWatchBoost,
        int cpvExclusionPenalty,
        int urgentDeadlineDays,
        int urgentDeadlinePenalty,
        int highRelevanceThreshold,
        string boampCron,
        string tedCron,
        string placeCron,
        DateTimeOffset updatedAt)
    {
        Id = id;
        PageSize = pageSize;
        PositiveSignalWeight = positiveSignalWeight;
        NegativeSignalPenalty = negativeSignalPenalty;
        PreferredDepartmentBoost = preferredDepartmentBoost;
        CpvWhitelistBoost = cpvWhitelistBoost;
        CpvWatchBoost = cpvWatchBoost;
        CpvExclusionPenalty = cpvExclusionPenalty;
        UrgentDeadlineDays = urgentDeadlineDays;
        UrgentDeadlinePenalty = urgentDeadlinePenalty;
        HighRelevanceThreshold = highRelevanceThreshold;
        BoampCron = boampCron;
        TedCron = tedCron;
        PlaceCron = placeCron;
        UpdatedAt = updatedAt;
    }

    public SourcingSettings(SourcingProfile profile, DateTimeOffset updatedAt)
        : this(DefaultId, profile.PageSize, profile.PositiveSignalWeight,
            profile.NegativeSignalPenalty, profile.PreferredDepartmentBoost,
            profile.CpvWhitelistBoost, profile.CpvWatchBoost, profile.CpvExclusionPenalty,
            profile.UrgentDeadlineDays, profile.UrgentDeadlinePenalty,
            profile.HighRelevanceThreshold, profile.BoampCron, profile.TedCron, profile.PlaceCron, updatedAt)
    {
        ChangeProfile(profile, updatedAt);
    }

    public int Id { get; private set; }

    public IReadOnlyList<string> Keywords => Array.AsReadOnly(_keywords);

    public IReadOnlyList<string> ExcludedKeywords => Array.AsReadOnly(_excludedKeywords);

    public IReadOnlyList<string> PositiveSignals => Array.AsReadOnly(_positiveSignals);

    public IReadOnlyList<string> NegativeSignals => Array.AsReadOnly(_negativeSignals);

    public IReadOnlyList<string> AllowedCountryCodes => Array.AsReadOnly(_allowedCountryCodes);

    public IReadOnlyList<string> PreferredDepartmentCodes => Array.AsReadOnly(_preferredDepartmentCodes);

    public IReadOnlyList<string> CpvWhitelistPrefixes => Array.AsReadOnly(_cpvWhitelistPrefixes);

    public IReadOnlyList<string> CpvWatchPrefixes => Array.AsReadOnly(_cpvWatchPrefixes);

    public IReadOnlyList<string> CpvExcludedPrefixes => Array.AsReadOnly(_cpvExcludedPrefixes);

    public int PageSize { get; private set; }

    public int PositiveSignalWeight { get; private set; }

    public int NegativeSignalPenalty { get; private set; }

    public int PreferredDepartmentBoost { get; private set; }

    public int CpvWhitelistBoost { get; private set; }

    public int CpvWatchBoost { get; private set; }

    public int CpvExclusionPenalty { get; private set; }

    public int UrgentDeadlineDays { get; private set; }

    public int UrgentDeadlinePenalty { get; private set; }

    public int HighRelevanceThreshold { get; private set; }

    public string BoampCron { get; private set; }

    public string TedCron { get; private set; }

    public string PlaceCron { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public void ChangeProfile(SourcingProfile profile, DateTimeOffset updatedAt)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var normalizedKeywords = NormalizeTerms(profile.Keywords, nameof(profile.Keywords));
        var normalizedExclusions = NormalizeTerms(profile.ExcludedKeywords, nameof(profile.ExcludedKeywords));

        if (normalizedKeywords.Length is < 1 or > 100)
        {
            throw new ArgumentException("Between 1 and 100 keywords are required.", nameof(profile));
        }

        if (normalizedExclusions.Length > 100)
        {
            throw new ArgumentException("At most 100 excluded keywords are allowed.", nameof(profile));
        }

        if (profile.PageSize is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(profile), "Page size must be between 1 and 100.");
        }

        ValidateScoreValue(profile.PositiveSignalWeight, nameof(profile.PositiveSignalWeight));
        ValidateScoreValue(profile.NegativeSignalPenalty, nameof(profile.NegativeSignalPenalty));
        ValidateScoreValue(profile.PreferredDepartmentBoost, nameof(profile.PreferredDepartmentBoost));
        ValidateScoreValue(profile.CpvWhitelistBoost, nameof(profile.CpvWhitelistBoost));
        ValidateScoreValue(profile.CpvWatchBoost, nameof(profile.CpvWatchBoost));
        ValidateScoreValue(profile.CpvExclusionPenalty, nameof(profile.CpvExclusionPenalty));
        ValidateScoreValue(profile.UrgentDeadlinePenalty, nameof(profile.UrgentDeadlinePenalty));
        ValidateScoreValue(profile.HighRelevanceThreshold, nameof(profile.HighRelevanceThreshold));

        if (profile.UrgentDeadlineDays is < 0 or > 365)
        {
            throw new ArgumentOutOfRangeException(nameof(profile), "Urgent deadline days must be between 0 and 365.");
        }

        var boampCron = NormalizeCron(profile.BoampCron, nameof(profile.BoampCron));
        var tedCron = NormalizeCron(profile.TedCron, nameof(profile.TedCron));
        var placeCron = NormalizeCron(profile.PlaceCron, nameof(profile.PlaceCron));

        if (updatedAt < UpdatedAt)
        {
            throw new ArgumentOutOfRangeException(nameof(updatedAt), "Settings updates must be chronological.");
        }

        _keywords = normalizedKeywords;
        _excludedKeywords = normalizedExclusions;
        _positiveSignals = NormalizeTerms(profile.PositiveSignals, nameof(profile.PositiveSignals));
        _negativeSignals = NormalizeTerms(profile.NegativeSignals, nameof(profile.NegativeSignals));
        _allowedCountryCodes = NormalizeCodes(
            profile.AllowedCountryCodes,
            nameof(profile.AllowedCountryCodes),
            3,
            3,
            char.IsLetter);
        _preferredDepartmentCodes = NormalizeCodes(
            profile.PreferredDepartmentCodes,
            nameof(profile.PreferredDepartmentCodes),
            1,
            3,
            char.IsLetterOrDigit);
        _cpvWhitelistPrefixes = NormalizeCodes(profile.CpvWhitelistPrefixes, nameof(profile.CpvWhitelistPrefixes), 2, 8, char.IsDigit);
        _cpvWatchPrefixes = NormalizeCodes(profile.CpvWatchPrefixes, nameof(profile.CpvWatchPrefixes), 2, 8, char.IsDigit);
        _cpvExcludedPrefixes = NormalizeCodes(profile.CpvExcludedPrefixes, nameof(profile.CpvExcludedPrefixes), 2, 8, char.IsDigit);
        PageSize = profile.PageSize;
        PositiveSignalWeight = profile.PositiveSignalWeight;
        NegativeSignalPenalty = profile.NegativeSignalPenalty;
        PreferredDepartmentBoost = profile.PreferredDepartmentBoost;
        CpvWhitelistBoost = profile.CpvWhitelistBoost;
        CpvWatchBoost = profile.CpvWatchBoost;
        CpvExclusionPenalty = profile.CpvExclusionPenalty;
        UrgentDeadlineDays = profile.UrgentDeadlineDays;
        UrgentDeadlinePenalty = profile.UrgentDeadlinePenalty;
        HighRelevanceThreshold = profile.HighRelevanceThreshold;
        BoampCron = boampCron;
        TedCron = tedCron;
        PlaceCron = placeCron;
        UpdatedAt = updatedAt;
    }

    private static string[] NormalizeTerms(IEnumerable<string> values, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(values, parameterName);
        var normalized = new List<string>();

        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 100)
            {
                throw new ArgumentException("Terms must contain between 1 and 100 characters.", parameterName);
            }

            normalized.Add(value.Trim());
        }

        return normalized
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string[] NormalizeCodes(
        IEnumerable<string> values,
        string parameterName,
        int minimumLength,
        int maximumLength,
        Func<char, bool> isAllowed)
    {
        var normalized = NormalizeTerms(values, parameterName);
        if (normalized.Any(value => value.Length < minimumLength
            || value.Length > maximumLength
            || value.Any(character => !isAllowed(character))))
        {
            throw new ArgumentException("One or more codes are invalid.", parameterName);
        }

        return normalized.Select(value => value.ToUpperInvariant()).ToArray();
    }

    private static void ValidateScoreValue(int value, string parameterName)
    {
        if (value is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Scoring values must be between 0 and 100.");
        }
    }

    private static string NormalizeCron(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 100)
        {
            throw new ArgumentException("Cron expressions must contain between 1 and 100 characters.", parameterName);
        }

        return value.Trim();
    }
}
