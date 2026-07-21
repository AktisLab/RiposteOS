using RiposteOS.Api.Consultations.Dtos;
using RiposteOS.Api.Consultations.Mappers;
using RiposteOS.Core.Consultations;
using RiposteOS.Infrastructure.Consultations;
using Microsoft.Extensions.Hosting;

namespace RiposteOS.Api.Consultations;

public static class ConsultationsEndpoints
{
    private const int MaxFilterLength = 2_000;
    private const int MaxOrderByLength = 200;

    public static IEndpointRouteBuilder MapConsultationsEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api").WithTags("Consultations");

        group.MapPost("/opportunities/{opportunityId:guid}/consultation", async Task<IResult> (
            Guid opportunityId,
            ConsultationsFacade consultations,
            CancellationToken cancellationToken) =>
        {
            var result = await consultations.PromoteOpportunityAsync(
                opportunityId,
                cancellationToken);
            if (result.Consultation is null)
            {
                return TypedResults.NotFound();
            }

            var response = ConsultationsMapper.ToConsultationResponse(result.Consultation);
            return result.Created
                ? TypedResults.Created($"/api/consultations/{response.Id}", response)
                : TypedResults.Ok(response);
        });

        group.MapPost("/consultations", async Task<IResult> (
            CreateConsultationRequest request,
            ConsultationsFacade consultations,
            CancellationToken cancellationToken) =>
        {
            var errors = Validate(request);
            if (errors.Count > 0)
            {
                return TypedResults.ValidationProblem(errors);
            }

            var consultation = await consultations.CreateAsync(
                request.Title,
                request.Buyer,
                request.ResponseDeadline,
                request.NoticeUrl,
                cancellationToken);
            var response = ConsultationsMapper.ToConsultationResponse(consultation);
            return TypedResults.Created($"/api/consultations/{response.Id}", response);
        });

        group.MapGet("/consultations", async Task<IResult> (
            [AsParameters] ConsultationListRequest request,
            ConsultationsFacade consultations,
            CancellationToken cancellationToken) =>
        {
            var errors = Validate(request);
            if (errors.Count > 0)
            {
                return TypedResults.ValidationProblem(errors);
            }

            var page = request.Page ?? 1;
            var pageSize = request.PageSize ?? 20;
            var result = await consultations.ListAsync(
                page,
                pageSize,
                request.Filter,
                request.OrderBy,
                cancellationToken);
            if (result.ValidationErrors.Length > 0)
            {
                return TypedResults.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["query"] = result.ValidationErrors,
                });
            }

            return TypedResults.Ok(new ConsultationListResponse(
                ConsultationsMapper.ToConsultationResponses(result.Items),
                result.TotalCount,
                page,
                pageSize));
        });

        group.MapGet("/consultations/{id:guid}", async Task<IResult> (
            Guid id,
            ConsultationsFacade consultations,
            CancellationToken cancellationToken) =>
        {
            var consultation = await consultations.GetAsync(id, cancellationToken);
            return consultation is null
                ? TypedResults.NotFound()
                : TypedResults.Ok(ConsultationsMapper.ToConsultationResponse(consultation));
        });

        group.MapGet("/consultations/{consultationId:guid}/documents", async Task<IResult> (
            Guid consultationId,
            ConsultationsFacade consultations,
            CancellationToken cancellationToken) =>
        {
            var documents = await consultations.ListDocumentsAsync(
                consultationId,
                cancellationToken);
            return documents is null
                ? TypedResults.NotFound()
                : TypedResults.Ok(ConsultationsMapper.ToDocumentResponses(documents));
        });

        group.MapPost("/consultations/{consultationId:guid}/documents", async Task<IResult> (
            Guid consultationId,
            AttachConsultationDocumentRequest request,
            ConsultationsFacade consultations,
            CancellationToken cancellationToken) =>
        {
            ConsultationDocumentKind? kind = null;
            if (!string.IsNullOrWhiteSpace(request.Kind))
            {
                if (!Enum.TryParse<ConsultationDocumentKind>(request.Kind, true, out var parsedKind)
                    || !Enum.IsDefined(parsedKind))
                {
                    return TypedResults.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["document"] = ["Le document ou son type métier est invalide."],
                    });
                }

                kind = parsedKind;
            }

            if (request.DocumentId == Guid.Empty)
            {
                return TypedResults.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["document"] = ["Le document ou son type métier est invalide."],
                });
            }

            var result = await consultations.AttachDocumentAsync(
                consultationId,
                request.DocumentId,
                kind,
                cancellationToken);
            if (result.Status is ConsultationDocumentAttachmentStatus.ConsultationNotFound
                or ConsultationDocumentAttachmentStatus.StoredDocumentNotFound)
            {
                return TypedResults.NotFound();
            }

            var response = ConsultationsMapper.ToDocumentResponse(
                result.Document
                ?? throw new InvalidOperationException("The attached document was not returned."));
            return result.Status == ConsultationDocumentAttachmentStatus.Created
                ? TypedResults.Created($"/api/consultations/{consultationId}/documents", response)
                : TypedResults.Ok(response);
        });

        group.MapPost("/consultations/{consultationId:guid}/documents/{documentId:guid}/analysis", async Task<IResult> (
            Guid consultationId,
            Guid documentId,
            ConsultationsFacade consultations,
            CancellationToken cancellationToken) =>
        {
            var result = await consultations.QueueDocumentProcessingAsync(
                consultationId,
                documentId,
                cancellationToken);
            if (result.Status == ConsultationDocumentProcessingStatus.DocumentNotFound)
            {
                return TypedResults.NotFound();
            }

            if (result.Status == ConsultationDocumentProcessingStatus.NotSupported)
            {
                return TypedResults.StatusCode(StatusCodes.Status415UnsupportedMediaType);
            }

            var response = ConsultationsMapper.ToDocumentResponse(
                result.Document ?? throw new InvalidOperationException("The document was not returned."));
            return result.Status == ConsultationDocumentProcessingStatus.Queued
                ? TypedResults.Accepted($"/api/consultations/{consultationId}/documents/{documentId}", response)
                : TypedResults.Ok(response);
        });

        group.MapPost("/consultations/{consultationId:guid}/documents/{documentId:guid}/classification", async Task<IResult> (
            Guid consultationId,
            Guid documentId,
            ConsultationsFacade consultations,
            CancellationToken cancellationToken) =>
        {
            var document = await consultations.RetryDocumentClassificationAsync(
                consultationId,
                documentId,
                cancellationToken);
            return document is null
                ? TypedResults.NotFound()
                : TypedResults.Accepted(
                    $"/api/consultations/{consultationId}/documents/{documentId}",
                    ConsultationsMapper.ToDocumentResponse(document));
        });

        group.MapPost("/consultations/{consultationId:guid}/documents/{documentId:guid}/embedding", async Task<IResult> (
            Guid consultationId,
            Guid documentId,
            ConsultationsFacade consultations,
            CancellationToken cancellationToken) =>
        {
            var document = await consultations.RetryDocumentEmbeddingAsync(consultationId, documentId, cancellationToken);
            return document is null
                ? TypedResults.NotFound()
                : TypedResults.Accepted(
                    $"/api/consultations/{consultationId}/documents/{documentId}",
                    ConsultationsMapper.ToDocumentResponse(document));
        });

        group.MapGet("/consultations/{consultationId:guid}/documents/{documentId:guid}/analysis/passages", async Task<IResult> (
            Guid consultationId,
            Guid documentId,
            ConsultationsFacade consultations,
            CancellationToken cancellationToken) =>
        {
            var passages = await consultations.ListDocumentPassagesAsync(
                consultationId,
                documentId,
                cancellationToken);
            return passages is null
                ? TypedResults.NotFound()
                : TypedResults.Ok(ConsultationsMapper.ToDocumentAnalysisPassageResponses(passages));
        });

        group.MapPut("/consultations/{consultationId:guid}/documents/{documentId:guid}", async Task<IResult> (
            Guid consultationId,
            Guid documentId,
            UpdateConsultationDocumentRequest request,
            ConsultationsFacade consultations,
            CancellationToken cancellationToken) =>
        {
            if (!TryParseDocumentKind(request.Kind, out var kind))
            {
                return TypedResults.ValidationProblem(new Dictionary<string, string[]>
                {
                    [nameof(request.Kind)] = ["Le type métier du document est invalide."],
                });
            }

            var document = await consultations.ChangeDocumentKindAsync(
                consultationId,
                documentId,
                kind,
                cancellationToken);
            return document is null
                ? TypedResults.NotFound()
                : TypedResults.Ok(ConsultationsMapper.ToDocumentResponse(document));
        });

        group.MapDelete("/consultations/{consultationId:guid}/documents/{documentId:guid}", async Task<IResult> (
            Guid consultationId,
            Guid documentId,
            ConsultationsFacade consultations,
            CancellationToken cancellationToken) =>
        {
            var detached = await consultations.DetachDocumentAsync(
                consultationId,
                documentId,
                cancellationToken);
            return detached ? TypedResults.NoContent() : TypedResults.NotFound();
        });

        return endpoints;
    }

    private static bool TryParseDocumentKind(string kindValue, out ConsultationDocumentKind kind) =>
        Enum.TryParse(kindValue, true, out kind) && Enum.IsDefined(kind);

    private static Dictionary<string, string[]> Validate(CreateConsultationRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.Title)
            || request.Title.Trim().Length > Consultation.MaximumTitleLength)
        {
            errors[nameof(request.Title)] =
                [$"Le titre doit contenir entre 1 et {Consultation.MaximumTitleLength} caractères."];
        }

        if (string.IsNullOrWhiteSpace(request.Buyer)
            || request.Buyer.Trim().Length > Consultation.MaximumBuyerLength)
        {
            errors[nameof(request.Buyer)] =
                [$"L’acheteur doit contenir entre 1 et {Consultation.MaximumBuyerLength} caractères."];
        }

        if (!string.IsNullOrWhiteSpace(request.NoticeUrl)
            && (request.NoticeUrl.Trim().Length > Consultation.MaximumNoticeUrlLength
                || !Uri.TryCreate(request.NoticeUrl.Trim(), UriKind.Absolute, out var uri)
                || (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))))
        {
            errors[nameof(request.NoticeUrl)] = ["L’URL de l’avis doit être une URL HTTP ou HTTPS absolue."];
        }

        return errors;
    }

    private static Dictionary<string, string[]> Validate(ConsultationListRequest request)
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
}
