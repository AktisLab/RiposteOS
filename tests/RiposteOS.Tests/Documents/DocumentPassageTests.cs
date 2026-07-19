using RiposteOS.Core.Documents;

namespace RiposteOS.Tests.Documents;

public sealed class DocumentPassageTests
{
    private static readonly Guid RunId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public void CreatesASourcedPassage()
    {
        var passage = new DocumentPassage(RunId, 1, " Texte ", 2, " Section ", " Feuille 1 ");

        Assert.Equal("Texte", passage.Text);
        Assert.Equal(2, passage.PageNumber);
        Assert.Equal("Section", passage.SectionTitle);
        Assert.Equal("Feuille 1", passage.SourceLocation);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    public void RejectsInvalidOrdinalAndPageNumber(int ordinal, int pageNumber) =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new DocumentPassage(RunId, ordinal, "Texte", pageNumber, null, null));

    [Fact]
    public void RejectsInvalidIdentifiersAndMetadata()
    {
        Assert.Throws<ArgumentException>(() => new DocumentPassage(Guid.Empty, 1, "Texte", null, null, null));
        Assert.Throws<ArgumentException>(() => new DocumentPassage(RunId, 1, " ", null, null, null));
        Assert.Throws<ArgumentException>(() => new DocumentPassage(RunId, 1, "Texte", null, " ", null));
        Assert.Throws<ArgumentException>(() => new DocumentPassage(RunId, 1, "Texte", null, null, new string('a', DocumentPassage.MaximumSourceLocationLength + 1)));
    }
}
