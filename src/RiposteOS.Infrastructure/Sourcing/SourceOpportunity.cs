namespace RiposteOS.Infrastructure.Sourcing;

public sealed record SourceOpportunity(
    string SourceId,
    string Title,
    string Buyer,
    DateOnly PublicationDate,
    DateTimeOffset? ResponseDeadline,
    string[] DepartmentCodes,
    string[] CpvCodes,
    string[] DescriptorCodes,
    string[] DescriptorLabels,
    string NoticeUrl,
    string RawPayload);
