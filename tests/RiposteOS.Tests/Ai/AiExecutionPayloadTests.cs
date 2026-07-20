using RiposteOS.Core.Ai;

namespace RiposteOS.Tests.Ai;

public sealed class AiExecutionPayloadTests
{
    [Fact]
    public void PayloadKeepsRawJsonOnce()
    {
        var payload = new AiExecutionPayload(Guid.NewGuid(), "{\"prompt\":\"bonjour\"}");

        payload.RecordOutput("{\"text\":\"réponse\"}");

        Assert.Equal("{\"prompt\":\"bonjour\"}", payload.Input);
        Assert.Equal("{\"text\":\"réponse\"}", payload.Output);
        Assert.Throws<InvalidOperationException>(() => payload.RecordOutput("{}"));
    }

    [Fact]
    public void PayloadRejectsInvalidJson()
    {
        Assert.Throws<ArgumentException>(() => new AiExecutionPayload(Guid.Empty, "{}"));
        Assert.ThrowsAny<System.Text.Json.JsonException>(() => new AiExecutionPayload(Guid.NewGuid(), "not-json"));
    }
}
