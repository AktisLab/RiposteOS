using RiposteOS.Core.Sourcing;

namespace RiposteOS.Tests.TestSupport;

internal static class TestSourcingProfiles
{
    public static SourcingProfile Create(
        IReadOnlyCollection<string>? keywords = null,
        IReadOnlyCollection<string>? excludedKeywords = null,
        int pageSize = 20) => new(
        keywords ?? ["logiciel"],
        excludedKeywords ?? [],
        ["logiciel métier"],
        ["porte automatique"],
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
        35);
}
