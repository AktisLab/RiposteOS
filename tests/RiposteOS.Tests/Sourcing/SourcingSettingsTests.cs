using RiposteOS.Core.Sourcing;
using RiposteOS.Tests.TestSupport;

namespace RiposteOS.Tests.Sourcing;

public sealed class SourcingSettingsTests
{
    private static readonly DateTimeOffset UpdatedAt =
        new(2026, 7, 15, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ProfileNormalizesValuesAndProtectsCollections()
    {
        var profile = TestSourcingProfiles.Create([" logiciel ", "LOGICIEL"], [" porte "])
            with
        {
            PreferredDepartmentCodes = [" 2a ", "69"],
            CpvWhitelistPrefixes = [" 722 "],
        };
        var settings = new SourcingSettings(profile, UpdatedAt);

        Assert.Equal(["logiciel"], settings.Keywords);
        Assert.Equal(["porte"], settings.ExcludedKeywords);
        Assert.Equal(["2A", "69"], settings.PreferredDepartmentCodes);
        Assert.Equal(["722"], settings.CpvWhitelistPrefixes);
        Assert.IsNotType<string[]>(settings.Keywords);
        Assert.Equal(20, settings.PageSize);
        Assert.Equal(35, settings.HighRelevanceThreshold);
    }

    [Theory]
    [MemberData(nameof(InvalidProfiles))]
    public void InvalidProfilesAreRejected(SourcingProfile profile)
    {
        Assert.ThrowsAny<ArgumentException>(() => new SourcingSettings(profile, UpdatedAt));
    }

    [Fact]
    public void ProfileUpdatesMustBeChronological()
    {
        var settings = new SourcingSettings(TestSourcingProfiles.Create(), UpdatedAt);

        Assert.Throws<ArgumentOutOfRangeException>(() => settings.ChangeProfile(
            TestSourcingProfiles.Create(["portail"]),
            UpdatedAt.AddTicks(-1)));
    }

    public static TheoryData<SourcingProfile> InvalidProfiles => new()
    {
        TestSourcingProfiles.Create([]),
        TestSourcingProfiles.Create(Enumerable.Range(1, 101).Select(index => $"keyword-{index}").ToArray()),
        TestSourcingProfiles.Create(excludedKeywords: Enumerable.Range(1, 101).Select(index => $"excluded-{index}").ToArray()),
        TestSourcingProfiles.Create(pageSize: 0),
        TestSourcingProfiles.Create(pageSize: 101),
        TestSourcingProfiles.Create([""]),
        TestSourcingProfiles.Create([new string('a', 101)]),
        TestSourcingProfiles.Create() with { PreferredDepartmentCodes = ["1234"] },
        TestSourcingProfiles.Create() with { CpvWhitelistPrefixes = ["abc"] },
        TestSourcingProfiles.Create() with { PositiveSignalWeight = 101 },
        TestSourcingProfiles.Create() with { UrgentDeadlineDays = 366 },
    };
}
