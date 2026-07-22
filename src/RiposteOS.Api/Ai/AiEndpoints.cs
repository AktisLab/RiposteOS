using Microsoft.AspNetCore.Mvc;
using RiposteOS.Api.Ai.Dtos;
using RiposteOS.Api.Ai.Mappers;
using RiposteOS.Core.Ai;
using RiposteOS.Infrastructure.Ai;

namespace RiposteOS.Api.Ai;

public static class AiEndpoints
{
    private const int MaxFilterLength = 2_000;
    private const int MaxOrderByLength = 200;

    public static IEndpointRouteBuilder MapAiEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/settings/ai").WithTags("IA");
        group.MapGet("/providers", async (AiFacade facade, CancellationToken ct) => TypedResults.Ok((await facade.ListProvidersAsync(ct)).Select(ToResponse)));
        group.MapPost("/providers", async Task<IResult> (AiProviderRequest request, AiFacade facade, CancellationToken ct) =>
        {
            if (!TryParse(request, out var protocol)) return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["provider"] = ["La configuration IA est invalide."] });
            var provider = await facade.CreateProviderAsync(request.Name, protocol, request.BaseUrl, request.Model, request.ApiKeyEnvironmentVariableName, request.IsEnabled, request.Capabilities, ct);
            return TypedResults.Created($"/api/settings/ai/providers/{provider.Id}", ToResponse(provider));
        });
        group.MapPut("/providers/{id:guid}", async Task<IResult> (Guid id, AiProviderRequest request, AiFacade facade, CancellationToken ct) =>
        {
            if (!TryParse(request, out var protocol)) return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["provider"] = ["La configuration IA est invalide."] });
            try { var provider = await facade.UpdateProviderAsync(id, request.Name, protocol, request.BaseUrl, request.Model, request.ApiKeyEnvironmentVariableName, request.IsEnabled, request.Capabilities, ct); return provider is null ? TypedResults.NotFound() : TypedResults.Ok(ToResponse(provider)); }
            catch (ArgumentException) { return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["provider"] = ["La configuration IA est invalide."] }); }
        });
        group.MapDelete("/providers/{id:guid}", async Task<IResult> (Guid id, AiFacade facade, CancellationToken ct) => await facade.DeleteProviderAsync(id, ct) ? TypedResults.NoContent() : TypedResults.Conflict());
        group.MapPut("/providers/{id:guid}/api-key", async Task<IResult> (Guid id, AiProviderApiKeyRequest request, AiFacade facade, CancellationToken ct) =>
        {
            try
            {
                return await facade.SetProviderApiKeyAsync(id, request.ApiKey, ct)
                    ? TypedResults.NoContent()
                    : TypedResults.NotFound();
            }
            catch (ArgumentException)
            {
                return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["apiKey"] = ["La clé API est invalide."] });
            }
        });
        group.MapDelete("/providers/{id:guid}/api-key", async Task<IResult> (Guid id, AiFacade facade, CancellationToken ct) => await facade.ClearProviderApiKeyAsync(id, ct) ? TypedResults.NoContent() : TypedResults.NotFound());
        group.MapPost("/providers/{id:guid}/test", async Task<IResult> (Guid id, AiFacade facade, CancellationToken ct) =>
        {
            var success = await facade.TestProviderAsync(id, ct);
            return success switch
            {
                null => TypedResults.NotFound(),
                true => TypedResults.NoContent(),
                false => TypedResults.Problem("Le fournisseur IA est indisponible.", statusCode: StatusCodes.Status502BadGateway),
            };
        });
        group.MapGet("/executions", async Task<IResult> (
            [AsParameters] AiExecutionLogListRequest request,
            AiFacade facade,
            CancellationToken ct) =>
        {
            var errors = ValidateList(request);
            if (errors.Count > 0)
            {
                return TypedResults.ValidationProblem(errors);
            }

            var page = request.Page ?? 1;
            var pageSize = request.PageSize ?? 20;
            var result = await facade.ListExecutionLogsAsync(page, pageSize, request.Filter, request.OrderBy, ct);
            return result.ValidationErrors.Length > 0
                ? TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["query"] = result.ValidationErrors })
                : TypedResults.Ok(new AiExecutionLogListResponse(
                    AiExecutionLogsMapper.ToResponses(result.Items),
                    result.TotalCount,
                    page,
                    pageSize));
        });
        group.MapGet("/executions/{id:guid}", async Task<IResult> (Guid id, AiFacade facade, CancellationToken ct) =>
        {
            var result = await facade.GetExecutionLogAsync(id, ct);
            return result is null
                ? TypedResults.NotFound()
                : TypedResults.Ok(new AiExecutionLogDetailsResponse(
                    AiExecutionLogsMapper.ToResponse(result.Execution),
                    result.Input,
                    result.Output));
        });
        group.MapGet("/tasks/{task}", async Task<IResult> (string task, AiFacade facade, CancellationToken ct) => Enum.TryParse<AiTask>(task, true, out var parsed) && Enum.IsDefined(parsed) ? (await facade.GetAssignmentAsync(parsed, ct) is { } assignment ? TypedResults.Ok(new AiTaskAssignmentResponse(assignment.Task.ToString(), assignment.ProviderId, assignment.UpdatedAt)) : TypedResults.NotFound()) : TypedResults.NotFound());
        group.MapPut("/tasks/{task}", async Task<IResult> (string task, AiTaskAssignmentRequest request, AiFacade facade, CancellationToken ct) => !Enum.TryParse<AiTask>(task, true, out var parsed) || !Enum.IsDefined(parsed) || request.ProviderId == Guid.Empty ? TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["task"] = ["L'affectation IA est invalide."] }) : await facade.AssignAsync(parsed, request.ProviderId, ct) ? TypedResults.NoContent() : TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["providerId"] = ["Le provider doit être actif."] }));
        return endpoints;
    }

    private static Dictionary<string, string[]> ValidateList(AiExecutionLogListRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (request.Page is < 1)
        {
            errors[nameof(request.Page)] = ["La page doit être supérieure ou égale à 1."];
        }

        if (request.PageSize is < 1 or > 100)
        {
            errors[nameof(request.PageSize)] = ["La taille de page doit être comprise entre 1 et 100."];
        }

        if (request.Filter?.Length > MaxFilterLength)
        {
            errors[nameof(request.Filter)] = ["Le filtre est trop long."];
        }

        if (request.OrderBy?.Length > MaxOrderByLength)
        {
            errors[nameof(request.OrderBy)] = ["Le tri est trop long."];
        }

        return errors;
    }

    private static bool TryParse(AiProviderRequest request, out AiProviderProtocol protocol) => Enum.TryParse(request.Protocol, true, out protocol) && Enum.IsDefined(protocol) && request.Capabilities is not AiProviderCapabilities.None && (request.Capabilities & ~(AiProviderCapabilities.Chat | AiProviderCapabilities.Embedding | AiProviderCapabilities.ToolCalling | AiProviderCapabilities.Reasoning)) == 0;
    private static AiProviderResponse ToResponse(AiProvider provider) => new(provider.Id, provider.Name, provider.Protocol.ToString(), provider.BaseUrl, provider.Model, provider.ApiKeyEnvironmentVariableName, provider.HasStoredApiKey, provider.IsEnabled, provider.Capabilities, provider.HealthStatus.ToString(), provider.HealthCheckedAt, provider.CreatedAt, provider.UpdatedAt);
}
public sealed record AiProviderRequest(string Name, string Protocol, string BaseUrl, string Model, string? ApiKeyEnvironmentVariableName, bool IsEnabled, AiProviderCapabilities Capabilities = AiProviderCapabilities.Chat);
public sealed record AiProviderResponse(Guid Id, string Name, string Protocol, string BaseUrl, string Model, string? ApiKeyEnvironmentVariableName, bool HasStoredApiKey, bool IsEnabled, AiProviderCapabilities Capabilities, string HealthStatus, DateTimeOffset? HealthCheckedAt, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
public sealed record AiProviderApiKeyRequest(string ApiKey);
public sealed record AiTaskAssignmentRequest(Guid ProviderId);
public sealed record AiTaskAssignmentResponse(string Task, Guid ProviderId, DateTimeOffset UpdatedAt);
