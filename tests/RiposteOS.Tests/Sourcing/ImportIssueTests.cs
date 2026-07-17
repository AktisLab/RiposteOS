using RiposteOS.Core.Sourcing;

namespace RiposteOS.Tests.Sourcing;

public sealed class ImportIssueTests
{
    [Fact]
    public void IssueCanBeResolvedOnce()
    {
        var createdAt = new DateTimeOffset(2026, 7, 16, 12, 0, 0, TimeSpan.Zero);
        var issue = new ImportIssue(
            Guid.NewGuid(),
            " boamp ",
            " 26-123 ",
            " mapping_json ",
            " {} ",
            createdAt);

        issue.Resolve(createdAt.AddMinutes(1));
        issue.Resolve(createdAt.AddMinutes(2));

        Assert.Equal(SourcingSource.Boamp, issue.Source);
        Assert.Equal("26-123", issue.SourceId);
        Assert.Equal("mapping_json", issue.ErrorCode);
        Assert.Equal(createdAt.AddMinutes(1), issue.ResolvedAt);
    }

    [Fact]
    public void IssueCannotResolveBeforeItWasCreated()
    {
        var createdAt = new DateTimeOffset(2026, 7, 16, 12, 0, 0, TimeSpan.Zero);
        var issue = new ImportIssue(
            Guid.NewGuid(),
            SourcingSource.Boamp,
            null,
            "mapping_json",
            "{}",
            createdAt);

        Assert.Throws<ArgumentOutOfRangeException>(() => issue.Resolve(createdAt.AddTicks(-1)));
    }
}
