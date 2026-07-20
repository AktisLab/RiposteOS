using System.IO.Compression;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using RiposteOS.Api.Documents.Dtos;
using RiposteOS.Api.Documents.Mappers;
using RiposteOS.Infrastructure.Documents;

namespace RiposteOS.Api.Documents;

public static class DocumentsEndpoints
{
    private const int MaxFilterLength = 2_000;
    private const int MaxOrderByLength = 200;

    public static IEndpointRouteBuilder MapDocumentsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/documents").WithTags("Documents");

        group.MapPost("", async Task<IResult> (
            HttpRequest request,
            DocumentsFacade documents,
            IOptions<ObjectStorageOptions> options,
            CancellationToken cancellationToken) =>
        {
            IFormFile? file;
            try
            {
                file = (await request.ReadFormAsync(cancellationToken)).Files.GetFile("file");
            }
            catch (InvalidDataException)
            {
                return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["file"] = ["Le fichier est invalide."] });
            }

            var validation = await ValidateUploadAsync(file, options.Value.MaxDocumentSizeBytes, cancellationToken);
            if (validation.Error is not null)
            {
                return validation.StatusCode is StatusCodes.Status413PayloadTooLarge or StatusCodes.Status415UnsupportedMediaType
                    ? TypedResults.StatusCode(validation.StatusCode)
                    : TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["file"] = [validation.Error] });
            }

            var uploadedFile = file!;
            await using var content = uploadedFile.OpenReadStream();
            try
            {
                var document = await documents.UploadAsync(
                    new DocumentUpload(uploadedFile.FileName, uploadedFile.ContentType, uploadedFile.Length, content),
                    cancellationToken);
                return TypedResults.Created($"/api/documents/{document.Id}", DocumentsMapper.ToDocumentResponse(document));
            }
            catch (ObjectStorageUnavailableException)
            {
                return TypedResults.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Le stockage objet est indisponible.");
            }
            catch (ArgumentException)
            {
                return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["file"] = ["Le fichier est invalide."] });
            }
        }).DisableAntiforgery().Accepts<IFormFile>("multipart/form-data").Produces<DocumentResponse>(StatusCodes.Status201Created).Produces(StatusCodes.Status413PayloadTooLarge).Produces(StatusCodes.Status415UnsupportedMediaType);

        group.MapGet("/{id:guid}", async Task<IResult> (Guid id, DocumentsFacade documents, CancellationToken cancellationToken) =>
        {
            var document = await documents.GetAsync(id, cancellationToken);
            return document is null ? TypedResults.NotFound() : TypedResults.Ok(DocumentsMapper.ToDocumentResponse(document));
        });

        group.MapGet("/{id:guid}/content", async Task<IResult> (Guid id, DocumentsFacade documents, CancellationToken cancellationToken) =>
        {
            try
            {
                var content = await documents.OpenContentAsync(id, cancellationToken);
                return content is null
                    ? TypedResults.NotFound()
                    : TypedResults.Stream(content.Content, content.Document.ContentType, content.Document.OriginalFileName);
            }
            catch (ObjectStorageUnavailableException)
            {
                return TypedResults.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Le stockage objet est indisponible.");
            }
        });

        group.MapGet("", async Task<IResult> (
            [AsParameters] DocumentListRequest request,
            DocumentsFacade documents,
            CancellationToken cancellationToken) =>
        {
            var errors = ValidateList(request);
            if (errors.Count > 0)
            {
                return TypedResults.ValidationProblem(errors);
            }

            var page = request.Page ?? 1;
            var pageSize = request.PageSize ?? 20;
            var result = await documents.ListAsync(page, pageSize, request.Filter, request.OrderBy, cancellationToken);
            return result.ValidationErrors.Length > 0
                ? TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["query"] = result.ValidationErrors })
                : TypedResults.Ok(new DocumentListResponse(DocumentsMapper.ToDocumentResponses(result.Items), result.TotalCount, page, pageSize));
        });

        return endpoints;
    }

    private static async Task<UploadValidation> ValidateUploadAsync(IFormFile? file, long maximumSize, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return new UploadValidation(StatusCodes.Status400BadRequest, "Un fichier non vide est requis.");
        }

        if (file.Length > maximumSize)
        {
            return new UploadValidation(StatusCodes.Status413PayloadTooLarge, "Le fichier dépasse la taille maximale autorisée.");
        }

        if (file.FileName.Length > 255 || file.FileName.IndexOfAny(['/', '\\']) >= 0 || file.FileName.Any(char.IsControl))
        {
            return new UploadValidation(StatusCodes.Status400BadRequest, "Le nom du fichier est invalide.");
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var format = Formats.SingleOrDefault(candidate => candidate.Extension == extension);
        if (format is null || !string.Equals(file.ContentType, format.ContentType, StringComparison.OrdinalIgnoreCase))
        {
            return new UploadValidation(StatusCodes.Status415UnsupportedMediaType, "Le format du fichier n'est pas pris en charge.");
        }

        await using var content = file.OpenReadStream();
        var header = new byte[8];
        var read = await content.ReadAsync(header, cancellationToken);
        if (!format.HasValidSignature(header.AsSpan(0, read)))
        {
            return new UploadValidation(StatusCodes.Status415UnsupportedMediaType, "La signature du fichier est invalide.");
        }

        if (format.RequiredEntry is not null)
        {
            try
            {
                content.Position = 0;
                using var archive = new ZipArchive(content, ZipArchiveMode.Read, leaveOpen: true);
                if (!archive.Entries.Any(entry => string.Equals(entry.FullName.Replace('\\', '/'), format.RequiredEntry, StringComparison.Ordinal)))
                {
                    return new UploadValidation(StatusCodes.Status415UnsupportedMediaType, "Le package OOXML est invalide.");
                }
            }
            catch (Exception exception) when (exception is InvalidDataException or ArgumentException)
            {
                return new UploadValidation(StatusCodes.Status415UnsupportedMediaType, "Le package OOXML est invalide.");
            }
        }

        return new UploadValidation(StatusCodes.Status200OK, null);
    }

    private static Dictionary<string, string[]> ValidateList(DocumentListRequest request)
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

    private sealed record UploadValidation(int StatusCode, string? Error);

    private sealed record FileFormat(string Extension, string ContentType, byte[] Signature, string? RequiredEntry)
    {
        public bool HasValidSignature(ReadOnlySpan<byte> header) => header.StartsWith(Signature);
    }

    private static readonly FileFormat[] Formats =
    [
        new(".pdf", "application/pdf", "%PDF-"u8.ToArray(), null),
        new(".doc", "application/msword", [0xD0, 0xCF, 0x11, 0xE0], null),
        new(".xls", "application/vnd.ms-excel", [0xD0, 0xCF, 0x11, 0xE0], null),
        new(".docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", [0x50, 0x4B, 0x03, 0x04], "word/document.xml"),
        new(".xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", [0x50, 0x4B, 0x03, 0x04], "xl/workbook.xml"),
        new(".zip", "application/zip", [0x50, 0x4B, 0x03, 0x04], null),
    ];
}
