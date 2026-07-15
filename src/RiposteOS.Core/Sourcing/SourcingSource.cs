namespace RiposteOS.Core.Sourcing;

public static class SourcingSource
{
    public const string Boamp = "BOAMP";

    public static string Normalize(string source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        return source.Trim().ToUpperInvariant();
    }
}
