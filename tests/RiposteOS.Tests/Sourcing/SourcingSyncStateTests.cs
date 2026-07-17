using RiposteOS.Core.Sourcing;

namespace RiposteOS.Tests.Sourcing;

public sealed class SourcingSyncStateTests
{
    [Fact]
    public void CursorAdvancesChronologically()
    {
        var firstUpdate = new DateTimeOffset(2026, 7, 15, 10, 0, 0, TimeSpan.Zero);
        var state = new SourcingSyncState(" boamp ");

        state.Advance(new DateOnly(2026, 7, 14), firstUpdate);
        state.Advance(new DateOnly(2026, 7, 15), firstUpdate.AddMinutes(1));

        Assert.Equal(SourcingSource.Boamp, state.Source);
        Assert.Equal(new DateOnly(2026, 7, 15), state.LastSuccessfulPublicationDate);
        Assert.Equal(firstUpdate.AddMinutes(1), state.UpdatedAt);
    }

    [Fact]
    public void CursorCannotMoveBackwardsInDateOrTime()
    {
        var firstUpdate = new DateTimeOffset(2026, 7, 15, 10, 0, 0, TimeSpan.Zero);
        var state = new SourcingSyncState(SourcingSource.Boamp);
        state.Advance(new DateOnly(2026, 7, 15), firstUpdate);

        Assert.Throws<ArgumentOutOfRangeException>(() => state.Advance(
            new DateOnly(2026, 7, 14),
            firstUpdate.AddMinutes(1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => state.Advance(
            new DateOnly(2026, 7, 15),
            firstUpdate.AddTicks(-1)));
    }
}
