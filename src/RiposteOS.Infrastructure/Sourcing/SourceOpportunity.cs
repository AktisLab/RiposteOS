namespace RiposteOS.Infrastructure.Sourcing;

public sealed record SourceOpportunity(
    string SourceId,
    string Title,
    string Buyer,
    DateOnly PublicationDate,
    DateTimeOffset? ResponseDeadline,
    string[] CountryCodes,
    string[] DepartmentCodes,
    string[] CpvCodes,
    string[] DescriptorCodes,
    string[] DescriptorLabels,
    string NoticeUrl,
    string RawPayload,
    string? Description = null,
    string? ProcedureType = null,
    string? ContractNature = null,
    decimal? EstimatedValue = null,
    string? Currency = null,
    string? ExecutionDuration = null,
    string? DocumentUrl = null,
    Guid? EformsNoticeId = null)
{
    public SourceOpportunityReference[] References { get; init; } = [];
}
