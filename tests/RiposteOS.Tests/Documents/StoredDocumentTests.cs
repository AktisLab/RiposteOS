using RiposteOS.Core.Documents;

namespace RiposteOS.Tests.Documents;

public sealed class StoredDocumentTests
{
    private static readonly Guid Id = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateTimeOffset CreatedAt = new(2026, 7, 17, 10, 0, 0, TimeSpan.Zero);
    private const string Hash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    [Fact]
    public void CreatesAnImmutableDocumentWithAnOpaqueStorageKey()
    {
        var document = new StoredDocument(Id, " Offre.pdf ", "APPLICATION/PDF", 12, Hash.ToUpperInvariant(), CreatedAt);

        Assert.Equal("Offre.pdf", document.OriginalFileName);
        Assert.Equal("application/pdf", document.ContentType);
        Assert.Equal(Hash, document.Sha256);
        Assert.Equal("documents/11111111111111111111111111111111/content", document.StorageKey);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1_073_741_825)]
    public void RejectsInvalidSizes(long size) => Assert.Throws<ArgumentOutOfRangeException>(() =>
        new StoredDocument(Id, "offre.pdf", "application/pdf", size, Hash, CreatedAt));

    [Theory]
    [InlineData("")]
    [InlineData("../offre.pdf")]
    public void RejectsInvalidNames(string name) => Assert.Throws<ArgumentException>(() =>
        new StoredDocument(Id, name, "application/pdf", 1, Hash, CreatedAt));

    [Fact]
    public void RejectsInvalidHash() => Assert.Throws<ArgumentException>(() =>
        new StoredDocument(Id, "offre.pdf", "application/pdf", 1, "not-a-hash", CreatedAt));
}
