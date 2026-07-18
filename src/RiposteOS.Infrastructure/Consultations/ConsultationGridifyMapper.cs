using Gridify;

namespace RiposteOS.Infrastructure.Consultations;

internal sealed class ConsultationGridifyMapper : GridifyMapper<ConsultationResult>
{
    public ConsultationGridifyMapper()
    {
        AddMap("id", consultation => consultation.Id);
        AddMap("title", consultation => consultation.Title);
        AddMap("buyer", consultation => consultation.Buyer);
        AddMap("responseDeadline", consultation => consultation.ResponseDeadline);
        AddMap("createdAt", consultation => consultation.CreatedAt);
        AddMap("source", consultation => consultation.Source);
    }
}
