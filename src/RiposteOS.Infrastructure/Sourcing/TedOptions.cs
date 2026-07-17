namespace RiposteOS.Infrastructure.Sourcing;

public sealed class TedOptions
{
    public const string SectionName = "Ted";

    public string BaseUrl { get; init; } = "https://api.ted.europa.eu/";

    public int InitialLookbackDays { get; init; } = 30;

    public int OverlapDays { get; init; } = 2;
}
