using RiposteOS.Core.Documents;

namespace RiposteOS.Tests.Documents;

public sealed class DocumentProcessingRunTests
{
    private static readonly Guid DocumentId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateTimeOffset ReferenceTime = new(2026, 7, 19, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void RunCompletesAndCannotBeRestarted()
    {
        var run = new DocumentProcessingRun(DocumentId, ReferenceTime);

        Assert.True(run.TryStart(ReferenceTime.AddMinutes(1)));
        run.Complete(3, 12, ReferenceTime.AddMinutes(2));

        Assert.Equal(DocumentProcessingStatus.Completed, run.Status);
        Assert.Equal(3, run.PageCount);
        Assert.Equal(12, run.PassageCount);
        Assert.False(run.TryStart(ReferenceTime.AddMinutes(3)));
        Assert.Throws<InvalidOperationException>(() => run.Retry(ReferenceTime.AddMinutes(3)));
    }

    [Fact]
    public void FailedRunCanBeRetriedWithResetCounters()
    {
        var run = new DocumentProcessingRun(DocumentId, ReferenceTime);

        run.Fail(" Le document ne peut pas être analysé. ", ReferenceTime.AddMinutes(1));
        run.Retry(ReferenceTime.AddMinutes(2));

        Assert.Equal(DocumentProcessingStatus.Queued, run.Status);
        Assert.Equal(0, run.PageCount);
        Assert.Equal(0, run.PassageCount);
        Assert.Null(run.ErrorMessage);
        Assert.Null(run.FailedAt);
    }

    [Fact]
    public void RejectsInvalidTransitionsDatesCountersAndMessages()
    {
        var run = new DocumentProcessingRun(DocumentId, ReferenceTime);

        Assert.Throws<ArgumentOutOfRangeException>(() => run.TryStart(ReferenceTime.AddTicks(-1)));
        Assert.Throws<InvalidOperationException>(() => run.Complete(0, 0, ReferenceTime));
        Assert.Throws<ArgumentException>(() => run.Fail(" ", ReferenceTime));
        Assert.Throws<ArgumentOutOfRangeException>(() => run.Fail(new string('a', DocumentProcessingRun.MaximumErrorMessageLength + 1), ReferenceTime));

        Assert.True(run.TryStart(ReferenceTime));
        Assert.Throws<ArgumentOutOfRangeException>(() => run.Complete(-1, 0, ReferenceTime));
        Assert.Throws<ArgumentOutOfRangeException>(() => run.Complete(0, -1, ReferenceTime));
        Assert.Throws<ArgumentOutOfRangeException>(() => run.Complete(0, 0, ReferenceTime.AddTicks(-1)));
    }
}
