namespace RiposteOS.Infrastructure.Sourcing;

public sealed class PlaceOptions
{
    public const string SectionName = "Place";

    public string BaseUrl { get; init; } = "https://www.marches-publics.gouv.fr/";

    public int InitialLookbackDays { get; init; } = 30;

    public int OverlapDays { get; init; } = 2;

    public int RequestDelayMilliseconds { get; init; } = 250;
}
