namespace RiposteOS.Api.Consultations.Dtos;

public sealed record DocumentEmbeddingResponse(
    string Status,
    int IndexedPassageCount,
    int PassageCount,
    string? ErrorMessage);
