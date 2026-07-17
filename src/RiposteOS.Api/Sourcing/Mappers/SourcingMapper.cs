using Riok.Mapperly.Abstractions;
using RiposteOS.Api.Sourcing.Dtos;
using RiposteOS.Core.Sourcing;

namespace RiposteOS.Api.Sourcing.Mappers;

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public static partial class SourcingMapper
{
    public static partial OpportunityListItem ToOpportunityListItem(Opportunity opportunity);

    public static partial OpportunityListItem[] ToOpportunityListItems(
        IEnumerable<Opportunity> opportunities);

    [MapProperty(nameof(ImportRun.Updated), nameof(ImportRunResponse.Changed))]
    public static partial ImportRunResponse ToImportRunResponse(ImportRun run);

    [MapProperty(nameof(ImportRun.Updated), nameof(ImportRunResponse.Changed))]
    public static partial ImportRunResponse[] ToImportRunResponses(IEnumerable<ImportRun> runs);

    [MapperIgnoreSource(nameof(ImportIssue.RawPayload))]
    public static partial ImportIssueResponse ToImportIssueResponse(ImportIssue issue);

    public static partial ImportIssueResponse[] ToImportIssueResponses(IEnumerable<ImportIssue> issues);

    public static partial SourcingSettingsResponse ToSourcingSettingsResponse(SourcingSettings settings);

    private static string MapStatus(ImportRunStatus status) => status.ToString();

    private static string MapStatus(OpportunityStatus status) => status.ToString();
}
