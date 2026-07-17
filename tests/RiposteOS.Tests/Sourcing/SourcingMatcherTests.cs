using RiposteOS.Core.Sourcing;
using RiposteOS.Tests.TestSupport;

namespace RiposteOS.Tests.Sourcing;

public sealed class SourcingMatcherTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 15, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void PositiveCpvTerritoryAndUrgencyRulesRemainExplainable()
    {
        var settings = new SourcingSettings(TestSourcingProfiles.Create(), Now);

        var match = SourcingMatcher.Evaluate(
            settings,
            "Développement d'un logiciel métier",
            ["69"],
            ["72200000", "48000000"],
            ["Logiciel"],
            Now.AddDays(3),
            Now);

        Assert.Equal(30, match.Score);
        Assert.Contains("+15 Signal positif : logiciel métier", match.Reasons);
        Assert.Contains("+25 CPV ciblé : 72200000", match.Reasons);
        Assert.Contains("+10 Territoire prioritaire : 69", match.Reasons);
        Assert.Contains("-20 Échéance sous 7 jours", match.Reasons);
        Assert.DoesNotContain(match.Reasons, reason => reason.Contains("surveillé", StringComparison.Ordinal));
    }

    [Fact]
    public void NegativeWatchExcludedAndExpiredRulesAreClamped()
    {
        var profile = TestSourcingProfiles.Create() with
        {
            CpvExcludedPrefixes = ["48"],
        };
        var settings = new SourcingSettings(profile, Now);

        var match = SourcingMatcher.Evaluate(
            settings,
            "Porte automatique avec logiciel",
            [],
            ["48000000"],
            [],
            Now.AddDays(-1),
            Now);

        Assert.Equal(0, match.Score);
        Assert.Contains("-30 Signal négatif : porte automatique", match.Reasons);
        Assert.Contains("+10 CPV surveillé : 48000000", match.Reasons);
        Assert.Contains("-50 CPV exclu : 48000000", match.Reasons);
        Assert.Contains("-20 Échéance dépassée", match.Reasons);
    }

    [Fact]
    public void ScoreIsCappedAtOneHundredAndMayHaveNoReason()
    {
        var profile = TestSourcingProfiles.Create() with
        {
            PositiveSignals = ["logiciel", "métier", "développement"],
            PositiveSignalWeight = 100,
        };
        var settings = new SourcingSettings(profile, Now);

        var high = SourcingMatcher.Evaluate(
            settings,
            "Développement logiciel métier",
            [],
            [],
            [],
            null,
            Now);
        var empty = SourcingMatcher.Evaluate(
            settings,
            "Fournitures courantes",
            [],
            [],
            [],
            null,
            Now);

        Assert.Equal(100, high.Score);
        Assert.Equal(0, empty.Score);
        Assert.Empty(empty.Reasons);
    }

    [Fact]
    public void SignalsMatchNormalizedWordsAndPhrasesWithoutSubstringFalsePositives()
    {
        var profile = TestSourcingProfiles.Create() with
        {
            PositiveSignals = ["API", "expérience utilisateur"],
            NegativeSignals = ["hébergement seul"],
        };
        var settings = new SourcingSettings(profile, Now);

        var match = SourcingMatcher.Evaluate(
            settings,
            "Conception d’une expérience-utilisateur et d'API sécurisées",
            [],
            [],
            ["HÉBERGEMENT SEUL"],
            null,
            Now);
        var falsePositive = SourcingMatcher.Evaluate(
            settings,
            "Fornitura di capitali e servizi sociali",
            [],
            [],
            [],
            null,
            Now);

        Assert.Equal(0, match.Score);
        Assert.Contains("+15 Signal positif : API", match.Reasons);
        Assert.Contains("+15 Signal positif : expérience utilisateur", match.Reasons);
        Assert.Contains("-30 Signal négatif : hébergement seul", match.Reasons);
        Assert.Empty(falsePositive.Reasons);
    }

    [Theory]
    [InlineData("Développement d’une API sécurisée", "api", true)]
    [InlineData("Fornitura di capitali", "api", false)]
    [InlineData("Expérience-utilisateur", "expérience utilisateur", true)]
    public void TermsCanBeMatchedWithTheSharedNormalizedBoundaryRule(
        string text,
        string term,
        bool expected) =>
        Assert.Equal(expected, SourcingMatcher.ContainsTerm(text, term));
}
