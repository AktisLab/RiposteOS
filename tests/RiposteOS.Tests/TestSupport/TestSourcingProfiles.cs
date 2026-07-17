using RiposteOS.Core.Sourcing;

namespace RiposteOS.Tests.TestSupport;

internal static class TestSourcingProfiles
{
    public static SourcingProfile Create(
        IReadOnlyCollection<string>? keywords = null,
        IReadOnlyCollection<string>? excludedKeywords = null,
        int pageSize = 20,
        string boampCron = SourcingSettings.DefaultSynchronizationCron,
        string tedCron = SourcingSettings.DefaultSynchronizationCron) => new(
        keywords ?? ["logiciel"],
        excludedKeywords ?? [],
        ["logiciel métier"],
        ["porte automatique"],
        ["FRA"],
        ["69"],
        ["72"],
        ["48000000"],
        [],
        pageSize,
        15,
        30,
        10,
        25,
        10,
        50,
        7,
        20,
        35,
        boampCron,
        tedCron);
}
