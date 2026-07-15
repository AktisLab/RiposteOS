using RiposteOS.Core.Sourcing;

namespace RiposteOS.Tests.Sourcing;

public sealed class ImportRunTests
{
    private static readonly DateTimeOffset ReferenceTime =
        new(2026, 7, 15, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void SuccessfulRunTracksProgressAndRejectsRestartAfterCompletion()
    {
        var queuedAt = new DateTimeOffset(2026, 7, 15, 10, 0, 0, TimeSpan.Zero);
        var run = new ImportRun(" boamp ", queuedAt);

        Assert.True(run.TryStart(queuedAt.AddMinutes(1)));
        Assert.True(run.TryStart(queuedAt.AddMinutes(2)));
        run.RecordProgress(new DateOnly(2026, 7, 15), 4, 2, 1, 0, queuedAt.AddMinutes(3));
        run.Complete(queuedAt.AddMinutes(4));

        Assert.Equal(SourcingSource.Boamp, run.Source);
        Assert.Equal(ImportRunStatus.Succeeded, run.Status);
        Assert.Equal(4, run.Fetched);
        Assert.Equal(2, run.Created);
        Assert.Equal(1, run.Updated);
        Assert.False(run.TryStart(queuedAt.AddMinutes(5)));
    }

    [Fact]
    public void SkippedRecordsProduceAPartiallyFailedRun()
    {
        var now = DateTimeOffset.UtcNow;
        var run = new ImportRun(SourcingSource.Boamp, now);

        run.TryStart(now);
        run.RecordProgress(DateOnly.FromDateTime(now.UtcDateTime), 1, 0, 0, 1, now);
        run.Complete(now);

        Assert.Equal(ImportRunStatus.PartiallyFailed, run.Status);
    }

    [Fact]
    public void RunCanFailBeforeItStarts()
    {
        var now = DateTimeOffset.UtcNow;
        var run = new ImportRun(SourcingSource.Boamp, now);

        run.Fail("failure", now.AddMinutes(1));

        Assert.Equal(ImportRunStatus.Failed, run.Status);
        Assert.Equal("failure", run.ErrorMessage);
        Assert.Equal(now.AddMinutes(1), run.FinishedAt);
    }

    [Fact]
    public void ProgressRequiresARunningImport()
    {
        var run = new ImportRun(SourcingSource.Boamp, ReferenceTime);

        var exception = Assert.Throws<InvalidOperationException>(() => run.RecordProgress(
            new DateOnly(2026, 7, 15),
            1,
            1,
            0,
            0,
            ReferenceTime));

        Assert.Contains("running", exception.Message);
    }

    [Theory]
    [InlineData(-1, 0, 0, 0)]
    [InlineData(1, -1, 0, 0)]
    [InlineData(1, 0, -1, 0)]
    [InlineData(1, 0, 0, -1)]
    public void ProgressCountersCannotBeNegative(int fetched, int created, int updated, int skipped)
    {
        var run = RunningImport();

        Assert.Throws<ArgumentOutOfRangeException>(() => run.RecordProgress(
            new DateOnly(2026, 7, 15),
            fetched,
            created,
            updated,
            skipped,
            ReferenceTime));
    }

    [Fact]
    public void ProgressCannotProcessMoreRecordsThanWereFetched()
    {
        var run = RunningImport();

        Assert.Throws<ArgumentException>(() => run.RecordProgress(
            new DateOnly(2026, 7, 15),
            1,
            1,
            1,
            0,
            ReferenceTime));
    }

    [Fact]
    public void TerminalTransitionsRejectInvalidStateAndTime()
    {
        var queued = new ImportRun(SourcingSource.Boamp, ReferenceTime);
        Assert.Throws<InvalidOperationException>(() => queued.Complete(ReferenceTime));
        Assert.Throws<ArgumentOutOfRangeException>(() => queued.TryStart(ReferenceTime.AddTicks(-1)));

        var running = RunningImport();
        Assert.Throws<ArgumentException>(() => running.Fail(" ", ReferenceTime));
        Assert.Throws<ArgumentOutOfRangeException>(() => running.RecordProgress(
            new DateOnly(2026, 7, 15),
            0,
            0,
            0,
            0,
            ReferenceTime.AddTicks(-1)));

        running.Complete(ReferenceTime);
        Assert.Throws<InvalidOperationException>(() => running.Fail("failure", ReferenceTime));
    }

    private static ImportRun RunningImport()
    {
        var run = new ImportRun(SourcingSource.Boamp, ReferenceTime);
        Assert.True(run.TryStart(ReferenceTime));
        return run;
    }
}
