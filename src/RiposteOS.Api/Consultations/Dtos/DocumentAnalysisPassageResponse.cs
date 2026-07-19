namespace RiposteOS.Api.Consultations.Dtos;

public sealed record DocumentAnalysisPassageResponse(
    int Ordinal,
    string Text,
    int? PageNumber,
    string? SectionTitle,
    string? SourceLocation);
