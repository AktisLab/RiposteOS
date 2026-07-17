using RiposteOS.Api.Sourcing.Dtos;
using RiposteOS.Api.Sourcing.Mappers;
using RiposteOS.Infrastructure.Sourcing;
using RiposteOS.Core.Sourcing;

namespace RiposteOS.Api.Sourcing;

public static class SourcingEndpoints
{
    private const int MaxFilterLength = 2_000;
    private const int MaxOrderByLength = 200;

    public static IEndpointRouteBuilder MapSourcingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api").WithTags("Sourcing");

        group.MapGet("/opportunities", async Task<IResult> (
            [AsParameters] OpportunityListRequest request,
            SourcingFacade sourcing,
            CancellationToken cancellationToken) =>
        {
            var errors = Validate(request);
            if (errors.Count > 0)
            {
                return TypedResults.ValidationProblem(errors);
            }

            var page = request.Page ?? 1;
            var pageSize = request.PageSize ?? 20;
            var result = await sourcing.ListOpportunitiesAsync(
                page,
                pageSize,
                request.Filter,
                request.OrderBy,
                request.Departments ?? [],
                request.Cpv,
                cancellationToken);
            if (result.ValidationErrors.Length > 0)
            {
                return TypedResults.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["query"] = result.ValidationErrors,
                });
            }

            return TypedResults.Ok(new OpportunityListResponse(
                SourcingMapper.ToOpportunityListItems(result.Items),
                result.TotalCount,
                page,
                pageSize));
        });

        group.MapPost("/sourcing/{source}/import", async Task<IResult> (
            string source,
            SourcingFacade sourcing,
            CancellationToken cancellationToken) =>
        {
            var result = await sourcing.QueueImportAsync(source, cancellationToken);
            if (result is null)
            {
                return TypedResults.NotFound();
            }

            if (result.Run is null)
            {
                return TypedResults.Conflict(new
                {
                    message = "Un profil de sourcing doit être créé avant de lancer une synchronisation.",
                });
            }

            if (!result.Created)
            {
                return TypedResults.Conflict(SourcingMapper.ToImportRunResponse(result.Run));
            }

            return TypedResults.Accepted(
                $"/api/sourcing/imports/{result.Run.Id}",
                SourcingMapper.ToImportRunResponse(result.Run));
        });

        group.MapPut("/opportunities/{id:guid}/status", async Task<IResult> (
            Guid id,
            OpportunityStatusRequest request,
            SourcingFacade sourcing,
            CancellationToken cancellationToken) =>
        {
            if (!Enum.TryParse<OpportunityStatus>(request.Status, true, out var status)
                || !Enum.IsDefined(status))
            {
                return TypedResults.ValidationProblem(new Dictionary<string, string[]>
                {
                    [nameof(request.Status)] = ["Le statut demandé est invalide."],
                });
            }

            var opportunity = await sourcing.UpdateOpportunityStatusAsync(id, status, cancellationToken);
            return opportunity is null
                ? TypedResults.NotFound()
                : TypedResults.Ok(SourcingMapper.ToOpportunityListItem(opportunity));
        });

        group.MapGet("/sourcing/imports", async Task<IResult> (
            [AsParameters] ImportRunListRequest request,
            SourcingFacade sourcing,
            CancellationToken cancellationToken) =>
        {
            var errors = ValidatePagination(request.Page, request.PageSize);
            if (errors.Count > 0)
            {
                return TypedResults.ValidationProblem(errors);
            }

            var page = request.Page ?? 1;
            var pageSize = request.PageSize ?? 10;
            var result = await sourcing.ListImportsAsync(page, pageSize, cancellationToken);
            return TypedResults.Ok(new ImportRunListResponse(
                SourcingMapper.ToImportRunResponses(result.Items),
                result.TotalCount,
                page,
                pageSize));
        });

        group.MapGet("/sourcing/imports/{id:guid}", async Task<IResult> (
            Guid id,
            SourcingFacade sourcing,
            CancellationToken cancellationToken) =>
        {
            var run = await sourcing.GetImportAsync(id, cancellationToken);

            return run is null
                ? TypedResults.NotFound()
                : TypedResults.Ok(SourcingMapper.ToImportRunResponse(run));
        });

        group.MapGet("/sourcing/imports/{id:guid}/issues", async Task<IResult> (
            Guid id,
            [AsParameters] ImportRunListRequest request,
            SourcingFacade sourcing,
            CancellationToken cancellationToken) =>
        {
            var errors = ValidatePagination(request.Page, request.PageSize);
            if (errors.Count > 0)
            {
                return TypedResults.ValidationProblem(errors);
            }

            var page = request.Page ?? 1;
            var pageSize = request.PageSize ?? 20;
            var result = await sourcing.ListImportIssuesAsync(
                id,
                page,
                pageSize,
                cancellationToken);
            return TypedResults.Ok(new ImportIssueListResponse(
                SourcingMapper.ToImportIssueResponses(result.Items),
                result.TotalCount,
                page,
                pageSize));
        });

        group.MapPost("/sourcing/import-issues/{id:guid}/retry", async Task<IResult> (
            Guid id,
            SourcingFacade sourcing,
            CancellationToken cancellationToken) =>
        {
            var result = await sourcing.RetryImportIssueAsync(id, cancellationToken);
            if (result is null)
            {
                return TypedResults.NotFound();
            }

            var response = SourcingMapper.ToImportIssueResponse(result.Issue);
            return result.Resolved
                ? TypedResults.Ok(response)
                : TypedResults.UnprocessableEntity(response);
        });

        group.MapGet("/sourcing/settings", async Task<IResult> (
            SourcingFacade sourcing,
            CancellationToken cancellationToken) =>
        {
            var settings = await sourcing.GetSettingsAsync(cancellationToken);

            return settings is null
                ? TypedResults.Content("null", "application/json")
                : TypedResults.Ok(SourcingMapper.ToSourcingSettingsResponse(settings));
        });

        group.MapPut("/sourcing/settings", async Task<IResult> (
            SourcingSettingsRequest request,
            SourcingFacade sourcing,
            CancellationToken cancellationToken) =>
        {
            var errors = Validate(request);
            if (errors.Count > 0)
            {
                return TypedResults.ValidationProblem(errors);
            }

            var settings = await sourcing.UpdateSettingsAsync(
                ToProfile(request),
                cancellationToken);
            if (settings is null)
            {
                return TypedResults.Conflict(new
                {
                    message = "Le profil ne peut pas être modifié pendant un import.",
                });
            }

            return TypedResults.Ok(SourcingMapper.ToSourcingSettingsResponse(settings));
        });

        return endpoints;
    }

    private static Dictionary<string, string[]> Validate(SourcingSettingsRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (request.Keywords is not { Length: > 0 and <= 100 })
        {
            errors[nameof(request.Keywords)] = ["Entre 1 et 100 mots-clés sont requis."];
        }
        else if (request.Keywords.Any(keyword => string.IsNullOrWhiteSpace(keyword) || keyword.Length > 100))
        {
            errors[nameof(request.Keywords)] = ["Chaque mot-clé doit contenir entre 1 et 100 caractères."];
        }

        if (request.ExcludedKeywords is { Length: > 100 }
            || request.ExcludedKeywords?.Any(keyword => string.IsNullOrWhiteSpace(keyword) || keyword.Length > 100) == true)
        {
            errors[nameof(request.ExcludedKeywords)] = ["Les exclusions sont limitées à 100 termes de 100 caractères."];
        }

        if (request.PageSize is < 1 or > 100)
        {
            errors[nameof(request.PageSize)] = ["La taille de page doit être comprise entre 1 et 100."];
        }

        ValidateTerms(request.PositiveSignals, nameof(request.PositiveSignals), errors);
        ValidateTerms(request.NegativeSignals, nameof(request.NegativeSignals), errors);
        ValidateCodes(request.AllowedCountryCodes, nameof(request.AllowedCountryCodes), 3, 3, char.IsLetter, errors);
        ValidateCodes(request.PreferredDepartmentCodes, nameof(request.PreferredDepartmentCodes), 1, 3, char.IsLetterOrDigit, errors);
        ValidateCodes(request.CpvWhitelistPrefixes, nameof(request.CpvWhitelistPrefixes), 2, 8, char.IsDigit, errors);
        ValidateCodes(request.CpvWatchPrefixes, nameof(request.CpvWatchPrefixes), 2, 8, char.IsDigit, errors);
        ValidateCodes(request.CpvExcludedPrefixes, nameof(request.CpvExcludedPrefixes), 2, 8, char.IsDigit, errors);

        ValidateScore(request.PositiveSignalWeight, nameof(request.PositiveSignalWeight), errors);
        ValidateScore(request.NegativeSignalPenalty, nameof(request.NegativeSignalPenalty), errors);
        ValidateScore(request.PreferredDepartmentBoost, nameof(request.PreferredDepartmentBoost), errors);
        ValidateScore(request.CpvWhitelistBoost, nameof(request.CpvWhitelistBoost), errors);
        ValidateScore(request.CpvWatchBoost, nameof(request.CpvWatchBoost), errors);
        ValidateScore(request.CpvExclusionPenalty, nameof(request.CpvExclusionPenalty), errors);
        ValidateScore(request.UrgentDeadlinePenalty, nameof(request.UrgentDeadlinePenalty), errors);
        ValidateScore(request.HighRelevanceThreshold, nameof(request.HighRelevanceThreshold), errors);

        if (request.UrgentDeadlineDays is < 0 or > 365)
        {
            errors[nameof(request.UrgentDeadlineDays)] = ["Le délai urgent doit être compris entre 0 et 365 jours."];
        }

        ValidateCron(request.BoampCron, nameof(request.BoampCron), errors);
        ValidateCron(request.TedCron, nameof(request.TedCron), errors);

        return errors;
    }

    private static SourcingProfile ToProfile(SourcingSettingsRequest request) => new(
        request.Keywords ?? [],
        request.ExcludedKeywords ?? [],
        request.PositiveSignals ?? [],
        request.NegativeSignals ?? [],
        request.AllowedCountryCodes ?? ["FRA"],
        request.PreferredDepartmentCodes ?? [],
        request.CpvWhitelistPrefixes ?? [],
        request.CpvWatchPrefixes ?? [],
        request.CpvExcludedPrefixes ?? [],
        request.PageSize,
        request.PositiveSignalWeight,
        request.NegativeSignalPenalty,
        request.PreferredDepartmentBoost,
        request.CpvWhitelistBoost,
        request.CpvWatchBoost,
        request.CpvExclusionPenalty,
        request.UrgentDeadlineDays,
        request.UrgentDeadlinePenalty,
        request.HighRelevanceThreshold,
        request.BoampCron ?? SourcingSettings.DefaultSynchronizationCron,
        request.TedCron ?? SourcingSettings.DefaultSynchronizationCron);

    private static void ValidateCron(
        string? cron,
        string name,
        Dictionary<string, string[]> errors)
    {
        if (cron is not null && !SourcingRecurringJobRegistrar.IsValidCron(cron))
        {
            errors[name] = ["Le CRON doit être une expression valide à 5 champs."];
        }
    }

    private static void ValidateTerms(
        string[]? values,
        string name,
        Dictionary<string, string[]> errors)
    {
        if (values is { Length: > 100 }
            || values?.Any(value => string.IsNullOrWhiteSpace(value) || value.Length > 100) == true)
        {
            errors[name] = ["La liste est limitée à 100 valeurs de 100 caractères."];
        }
    }

    private static void ValidateCodes(
        string[]? values,
        string name,
        int minimumLength,
        int maximumLength,
        Func<char, bool> isAllowed,
        Dictionary<string, string[]> errors)
    {
        ValidateTerms(values, name, errors);
        if (values?.Any(value => value.Length < minimumLength
            || value.Length > maximumLength
            || value.Any(character => !isAllowed(character))) == true)
        {
            errors[name] = ["Un ou plusieurs codes sont invalides."];
        }
    }

    private static void ValidateScore(
        int value,
        string name,
        Dictionary<string, string[]> errors)
    {
        if (value is < 0 or > 100)
        {
            errors[name] = ["La valeur doit être comprise entre 0 et 100."];
        }
    }

    private static Dictionary<string, string[]> Validate(OpportunityListRequest request)
    {
        var errors = ValidatePagination(request.Page, request.PageSize);

        if (request.Filter?.Length > MaxFilterLength)
        {
            errors[nameof(request.Filter)] = ["Le filtre est trop long."];
        }

        if (request.OrderBy?.Length > MaxOrderByLength)
        {
            errors[nameof(request.OrderBy)] = ["Le tri est trop long."];
        }

        if (request.Departments is { Length: > 100 }
            || request.Departments?.Any(code => code.Length is < 1 or > 3
                || code.Any(character => !char.IsLetterOrDigit(character))) == true)
        {
            errors[nameof(request.Departments)] = ["Les départements demandés sont invalides."];
        }

        if (request.Cpv is { } cpv
            && (cpv.Length is < 2 or > 8 || cpv.Any(character => !char.IsDigit(character))))
        {
            errors[nameof(request.Cpv)] = ["Le préfixe CPV demandé est invalide."];
        }

        return errors;
    }

    private static Dictionary<string, string[]> ValidatePagination(int? page, int? pageSize)
    {
        var errors = new Dictionary<string, string[]>();

        if (page is < 1)
        {
            errors["Page"] = ["La page doit être supérieure ou égale à 1."];
        }

        if (pageSize is < 1 or > 100)
        {
            errors["PageSize"] = ["La taille de page doit être comprise entre 1 et 100."];
        }

        return errors;
    }
}
