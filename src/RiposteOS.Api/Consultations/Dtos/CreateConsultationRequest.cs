namespace RiposteOS.Api.Consultations.Dtos;

public sealed record CreateConsultationRequest(
    string Title,
    string Buyer,
    DateTimeOffset? ResponseDeadline,
    string? NoticeUrl);
