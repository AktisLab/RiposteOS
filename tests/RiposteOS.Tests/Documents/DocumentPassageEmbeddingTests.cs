using RiposteOS.Core.Documents;

namespace RiposteOS.Tests.Documents;

public sealed class DocumentPassageEmbeddingTests
{
    [Fact]
    public void RejectsAnEmbeddingWithAnUnexpectedDimension()
    {
        var embedding = new DocumentPassageEmbedding(Guid.NewGuid(), new string('a', 64), "Ollama", "qwen3-embedding:0.6b", DateTimeOffset.UtcNow);
        embedding.TryStart(DateTimeOffset.UtcNow);

        Assert.Throws<ArgumentException>(() => embedding.Complete([1f], DateTimeOffset.UtcNow));
    }

    [Fact]
    public void SupportsRetryAfterFailureAndDoesNotOverwriteACompletedEmbedding()
    {
        var now = DateTimeOffset.UtcNow;
        var embedding = new DocumentPassageEmbedding(Guid.NewGuid(), new string('a', 64), "Ollama", "qwen3-embedding:0.6b", now);

        Assert.True(embedding.TryStart(now));
        Assert.False(embedding.TryStart(now));
        embedding.Fail("indisponible", now);
        Assert.True(embedding.TryStart(now));
        embedding.Complete(Vector(), now);
        embedding.Fail("ignoré", now);

        Assert.True(embedding.Matches(new string('a', 64), "Ollama", "qwen3-embedding:0.6b"));
        Assert.False(embedding.Matches(new string('b', 64), "Ollama", "qwen3-embedding:0.6b"));
        Assert.Equal(DocumentPassageEmbeddingStatus.Completed, embedding.Status);
    }

    [Fact]
    public void ValidatesTheEmbeddingIdentityAndTransition()
    {
        Assert.Throws<ArgumentException>(() => new DocumentPassageEmbedding(Guid.Empty, new string('a', 64), "Ollama", "qwen", DateTimeOffset.UtcNow));
        Assert.Throws<ArgumentException>(() => new DocumentPassageEmbedding(Guid.NewGuid(), " ", "Ollama", "qwen", DateTimeOffset.UtcNow));
        var embedding = new DocumentPassageEmbedding(Guid.NewGuid(), new string('a', 64), "Ollama", "qwen", DateTimeOffset.UtcNow);

        Assert.Throws<InvalidOperationException>(() => embedding.Complete(Vector(), DateTimeOffset.UtcNow));
    }

    private static float[] Vector()
    {
        var value = new float[DocumentPassageEmbedding.ExpectedDimension];
        value[0] = 1;
        return value;
    }
}
