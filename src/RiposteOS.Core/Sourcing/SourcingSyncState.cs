namespace RiposteOS.Core.Sourcing;

public sealed class SourcingSyncState
{
    private SourcingSyncState(
        string source,
        DateOnly? lastSuccessfulPublicationDate,
        DateTimeOffset? updatedAt)
    {
        Source = source;
        LastSuccessfulPublicationDate = lastSuccessfulPublicationDate;
        UpdatedAt = updatedAt;
    }

    public SourcingSyncState(string source)
        : this(SourcingSource.Normalize(source), null, null)
    {
    }

    public string Source { get; private set; }

    public DateOnly? LastSuccessfulPublicationDate { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public void Advance(DateOnly publicationDate, DateTimeOffset updatedAt)
    {
        if (LastSuccessfulPublicationDate is { } previousDate && publicationDate < previousDate)
        {
            throw new ArgumentOutOfRangeException(
                nameof(publicationDate),
                "A sourcing cursor cannot move backwards.");
        }

        if (UpdatedAt is { } previousUpdate && updatedAt < previousUpdate)
        {
            throw new ArgumentOutOfRangeException(nameof(updatedAt), "Cursor updates must be chronological.");
        }

        LastSuccessfulPublicationDate = publicationDate;
        UpdatedAt = updatedAt;
    }
}
