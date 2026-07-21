namespace RiposteOS.Core.Consultations;

public sealed class ConsultationAssistantMessageCitation
{
    public ConsultationAssistantMessageCitation(Guid messageId, Guid documentPassageId)
    {
        MessageId = messageId == Guid.Empty ? throw new ArgumentException("A message is required.", nameof(messageId)) : messageId;
        DocumentPassageId = documentPassageId == Guid.Empty ? throw new ArgumentException("A passage is required.", nameof(documentPassageId)) : documentPassageId;
    }

    public Guid MessageId { get; private set; }
    public Guid DocumentPassageId { get; private set; }
}
