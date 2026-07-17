namespace RiposteOS.Infrastructure.Sourcing;

public sealed class BoampOptions
{
    public const string SectionName = "Boamp";

    public string BaseUrl { get; init; } =
        "https://boamp-datadila.opendatasoft.com/api/explore/v2.1/catalog/datasets/boamp/";

    public int InitialLookbackDays { get; init; } = 30;

    public int OverlapDays { get; init; } = 2;
}
