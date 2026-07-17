namespace RiposteOS.Infrastructure.Sourcing;

public sealed record PlaceSearchResult(
    DateOnly PublicationDate,
    int Fetched,
    IReadOnlyList<SourceOpportunity> Opportunities,
    int Skipped,
    IReadOnlyList<SourceImportIssue> Issues);

internal sealed record PlaceSearchItem(
    string SourceId,
    string OrganizationCode,
    string Title,
    string Buyer,
    DateOnly PublicationDate,
    string? Description,
    string? ProcedureType,
    string? ContractNature,
    string[] DepartmentCodes,
    string NoticeUrl);

internal sealed record PlaceSnapshot(
    string SourceId,
    string Title,
    string Buyer,
    DateOnly PublicationDate,
    DateTimeOffset? ResponseDeadline,
    string[] DepartmentCodes,
    string[] CpvCodes,
    string NoticeUrl,
    string? Description,
    string? ProcedureType,
    string? ContractNature,
    string? DocumentUrl,
    SourceOpportunityReference[] References);

internal sealed record PlaceRawRecord(
    PlaceSearchItem SearchItem,
    string DetailHtml);
