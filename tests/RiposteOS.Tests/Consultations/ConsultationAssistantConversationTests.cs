using RiposteOS.Core.Consultations;

namespace RiposteOS.Tests.Consultations;

public sealed class ConsultationAssistantConversationTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 20, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ConversationCanBeRenamedThenArchived()
    {
        var conversation = new ConsultationAssistantConversation(Guid.NewGuid(), "Première question", Now);

        conversation.Rename("Exigences techniques", Now.AddMinutes(1));
        conversation.Archive(Now.AddMinutes(2));

        Assert.Equal("Exigences techniques", conversation.Title);
        Assert.Equal(Now.AddMinutes(2), conversation.ArchivedAt);
        Assert.Throws<InvalidOperationException>(() => conversation.Rename("Autre", Now.AddMinutes(3)));
    }

    [Fact]
    public void AssistantMessageRequiresACompletedStateBeforeContentIsExposed()
    {
        var message = ConsultationAssistantMessage.StartAssistant(Guid.NewGuid(), Now);

        message.Complete("Réponse sourcée", "Ollama", "gpt-oss:20b", Now.AddSeconds(1));

        Assert.Equal(ConsultationAssistantMessageStatus.Completed, message.Status);
        Assert.Equal("Réponse sourcée", message.Content);
        Assert.Equal("Ollama", message.ProviderName);
    }

    [Fact]
    public void AssistantMessagesValidateInputAndKeepTerminalStatesStable()
    {
        Assert.Throws<ArgumentException>(() => ConsultationAssistantMessage.CreateUser(Guid.Empty, "Question", Now));
        Assert.Throws<ArgumentException>(() => ConsultationAssistantMessage.CreateUser(Guid.NewGuid(), " ", Now));
        Assert.Throws<ArgumentException>(() => ConsultationAssistantMessage.CreateUser(Guid.NewGuid(), new string('q', ConsultationAssistantMessage.MaximumContentLength + 1), Now));

        var pending = ConsultationAssistantMessage.StartAssistant(Guid.NewGuid(), Now);
        Assert.Throws<ArgumentException>(() => pending.Complete(" ", "Ollama", "model", Now));
        Assert.Throws<ArgumentException>(() => pending.Complete("Réponse", " ", "model", Now));
        Assert.Throws<ArgumentException>(() => pending.Complete("Réponse", "Ollama", " ", Now));
        pending.Fail("Arrêtée", Now.AddSeconds(1), cancelled: true);
        pending.Fail("Ignorée", Now.AddSeconds(2));

        Assert.Equal(ConsultationAssistantMessageStatus.Cancelled, pending.Status);
        Assert.Equal("Arrêtée", pending.ErrorMessage);
        Assert.Equal(Now.AddSeconds(1), pending.FailedAt);
        Assert.Throws<InvalidOperationException>(() => pending.Complete("Réponse", "Ollama", "model", Now.AddSeconds(3)));
    }

    [Fact]
    public void AssistantMessageCitationsRequireBothIdentifiers()
    {
        Assert.Throws<ArgumentException>(() => new ConsultationAssistantMessageCitation(Guid.Empty, Guid.NewGuid()));
        Assert.Throws<ArgumentException>(() => new ConsultationAssistantMessageCitation(Guid.NewGuid(), Guid.Empty));

        var citation = new ConsultationAssistantMessageCitation(Guid.NewGuid(), Guid.NewGuid());

        Assert.NotEqual(Guid.Empty, citation.MessageId);
        Assert.NotEqual(Guid.Empty, citation.DocumentPassageId);
    }
}
