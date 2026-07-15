namespace RiposteOS.Api.Sourcing.Dtos;

public sealed record OpportunityListItem(
    Guid Id,
    string Source,
    string SourceId,
    string Title,
    string Buyer,
    int MatchScore,
    string Status,
    DateOnly PublicationDate,
    DateTimeOffset? ResponseDeadline,
    string[] DepartmentCodes,
    string[] CpvCodes,
    string[] DescriptorLabels,
    string[] MatchReasons,
    string NoticeUrl,
    DateTimeOffset UpdatedAt);
