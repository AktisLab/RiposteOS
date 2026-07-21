using System.Text.Json;
using RiposteOS.Api.Consultations.Dtos;
using RiposteOS.Infrastructure.Consultations;

using RiposteOS.Infrastructure.Consultations.Assistant;

namespace RiposteOS.Api.Consultations;

public static class ConsultationAssistantEndpoints
{
    public static IEndpointRouteBuilder MapConsultationAssistantEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/consultations/{consultationId:guid}/assistant").WithTags("Assistant de consultation");
        group.MapGet("/conversations", async Task<IResult> (Guid consultationId, ConsultationAssistantFacade assistant, CancellationToken ct) => (await assistant.ListAsync(consultationId, ct)) is { } conversations ? TypedResults.Ok(conversations) : TypedResults.NotFound());
        group.MapPost("/conversations", async Task<IResult> (Guid consultationId, CreateAssistantConversationRequest request, ConsultationAssistantFacade assistant, CancellationToken ct) =>
        {
            try { var conversation = await assistant.CreateAsync(consultationId, request.Title, ct); return conversation is null ? TypedResults.NotFound() : TypedResults.Created($"/api/consultations/{consultationId}/assistant/conversations/{conversation.Id}", conversation); }
            catch (ArgumentException) { return TypedResults.ValidationProblem(new Dictionary<string, string[]> { [nameof(request.Title)] = ["Le titre de conversation est invalide."] }); }
        });
        group.MapGet("/conversations/{conversationId:guid}", async Task<IResult> (Guid consultationId, Guid conversationId, ConsultationAssistantFacade assistant, CancellationToken ct) => (await assistant.GetAsync(consultationId, conversationId, ct)) is { } conversation ? TypedResults.Ok(conversation) : TypedResults.NotFound());
        group.MapMethods("/conversations/{conversationId:guid}", ["PATCH"], async Task<IResult> (Guid consultationId, Guid conversationId, UpdateAssistantConversationRequest request, ConsultationAssistantFacade assistant, CancellationToken ct) =>
        {
            try { return await assistant.RenameAsync(consultationId, conversationId, request.Title, ct) ? TypedResults.NoContent() : TypedResults.NotFound(); }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException) { return TypedResults.ValidationProblem(new Dictionary<string, string[]> { [nameof(request.Title)] = ["Le titre de conversation est invalide."] }); }
        });
        group.MapPost("/conversations/{conversationId:guid}/archive", async Task<IResult> (Guid consultationId, Guid conversationId, ConsultationAssistantFacade assistant, CancellationToken ct) => await assistant.ArchiveAsync(consultationId, conversationId, ct) ? TypedResults.NoContent() : TypedResults.NotFound());
        group.MapPost("/conversations/{conversationId:guid}/messages", async (Guid consultationId, Guid conversationId, CreateAssistantMessageRequest request, HttpResponse response, ConsultationAssistantFacade assistant, CancellationToken ct) =>
        {
            await WriteStreamAsync(response, assistant.SendAsync(consultationId, conversationId, request.Content, ct), ct);
        });
        group.MapPost("/conversations/{conversationId:guid}/messages/{userMessageId:guid}/retry", async (Guid consultationId, Guid conversationId, Guid userMessageId, HttpResponse response, ConsultationAssistantFacade assistant, CancellationToken ct) =>
        {
            await WriteStreamAsync(response, assistant.RetryAsync(consultationId, conversationId, userMessageId, ct), ct);
        });
        return endpoints;
    }

    private static async Task WriteStreamAsync(
        HttpResponse response,
        IAsyncEnumerable<ConsultationAssistantStreamEvent> stream,
        CancellationToken cancellationToken)
    {
        response.StatusCode = StatusCodes.Status200OK;
        response.ContentType = "text/event-stream";
        response.Headers.CacheControl = "no-cache";
        await foreach (var item in stream.WithCancellation(cancellationToken))
        {
            await response.WriteAsync($"event: {item.Type}\ndata: {JsonSerializer.Serialize(item, JsonSerializerOptions.Web)}\n\n", cancellationToken);
            await response.Body.FlushAsync(cancellationToken);
        }
    }
}
