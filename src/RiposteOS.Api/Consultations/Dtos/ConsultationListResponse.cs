namespace RiposteOS.Api.Consultations.Dtos;

public sealed record ConsultationListResponse(
    ConsultationResponse[] Items,
    int TotalCount,
    int Page,
    int PageSize);
