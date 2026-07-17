using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.IO.Compression;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using RiposteOS.Api.Documents.Dtos;
using RiposteOS.Core.Documents;
using RiposteOS.Infrastructure.Persistence;
using RiposteOS.Tests.TestSupport;

namespace RiposteOS.Tests.Api;

public sealed class DocumentsEndpointsTests(RiposteWebApplicationFactory factory)
    : IClassFixture<RiposteWebApplicationFactory>
{
    [Fact]
    public async Task DocumentsAreListedWithDefaultDeterministicOrderingAndPagination()
    {
        await factory.ResetAsync();
        var createdAt = new DateTimeOffset(2026, 7, 17, 10, 0, 0, TimeSpan.Zero);
        var first = new StoredDocument(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "a.pdf",
            "application/pdf",
            1,
            new string('a', 64),
            createdAt);
        var second = new StoredDocument(
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            "b.pdf",
            "application/pdf",
            1,
            new string('b', 64),
            createdAt);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<RiposteDbContext>();
            dbContext.Set<StoredDocument>().AddRange(first, second);
            await dbContext.SaveChangesAsync();
        }

        var client = factory.CreateClient();
        var page = await client.GetFromJsonAsync<DocumentListResponse>("/api/documents?page=1&pageSize=1");
        using var internalFilter = await client.GetAsync("/api/documents?filter=storageKey=*private");

        Assert.Equal(2, page!.TotalCount);
        Assert.Equal(first.Id, Assert.Single(page.Items).Id);
        Assert.Equal(HttpStatusCode.BadRequest, internalFilter.StatusCode);
    }

    [Fact]
    public async Task PdfUploadCanBeListedAndDownloadedWithoutExposingStorageMetadata()
    {
        await factory.ResetAsync();
        var bytes = "%PDF-1.7\ncontenu"u8.ToArray();
        using var form = CreateForm("offre.pdf", "application/pdf", bytes);
        var client = factory.CreateClient();

        using var uploaded = await client.PostAsync("/api/documents", form);
        var document = await uploaded.Content.ReadFromJsonAsync<DocumentResponse>();
        var list = await client.GetFromJsonAsync<DocumentListResponse>("/api/documents?page=1&pageSize=5");
        using var metadata = await client.GetAsync($"/api/documents/{document!.Id}");
        using var downloaded = await client.GetAsync($"/api/documents/{document.Id}/content");

        Assert.Equal(HttpStatusCode.Created, uploaded.StatusCode);
        Assert.Equal(1, list!.TotalCount);
        Assert.Equal(bytes, await downloaded.Content.ReadAsByteArrayAsync());
        Assert.DoesNotContain("storageKey", await metadata.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UnsupportedUploadReturns415()
    {
        await factory.ResetAsync();
        using var form = CreateForm("offre.exe", "application/octet-stream", "MZ");

        using var response = await factory.CreateClient().PostAsync("/api/documents", form);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/documents?page=0")]
    [InlineData("/api/documents?pageSize=0")]
    [InlineData("/api/documents?pageSize=101")]
    public async Task InvalidPaginationReturns400(string path)
    {
        await factory.ResetAsync();

        using var response = await factory.CreateClient().GetAsync(path);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ExcessiveFilterAndOrderByReturn400()
    {
        await factory.ResetAsync();
        var filter = new string('a', 2_001);
        var orderBy = new string('a', 201);

        using var response = await factory.CreateClient().GetAsync($"/api/documents?filter={filter}&orderBy={orderBy}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task MissingAndEmptyUploadsReturn400()
    {
        await factory.ResetAsync();
        using var missing = await factory.CreateClient().PostAsync("/api/documents", new MultipartFormDataContent());
        using var emptyForm = CreateForm("offre.pdf", "application/pdf", Array.Empty<byte>());
        using var empty = await factory.CreateClient().PostAsync("/api/documents", emptyForm);

        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
    }

    [Fact]
    public async Task NonMultipartUploadReturns415()
    {
        await factory.ResetAsync();
        using var content = new StringContent("not a multipart form");

        using var response = await factory.CreateClient().PostAsync("/api/documents", content);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }

    [Fact]
    public async Task OversizedUploadReturns413()
    {
        await using var limitedFactory = factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["ObjectStorage:MaxDocumentSizeBytes"] = "1" })));
        using var form = CreateForm("offre.pdf", "application/pdf", "%PDF-1.7");

        using var response = await limitedFactory.CreateClient().PostAsync("/api/documents", form);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    [Theory]
    [InlineData("../offre.pdf")]
    [InlineData("offre/2026.pdf")]
    [InlineData("offre\\2026.pdf")]
    public async Task InvalidUploadFileNamesReturn400(string fileName)
    {
        await factory.ResetAsync();
        using var form = CreateForm(fileName, "application/pdf", "%PDF-1.7");

        using var response = await factory.CreateClient().PostAsync("/api/documents", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task OverlongUploadFileNameReturns400()
    {
        await factory.ResetAsync();
        using var form = CreateForm($"{new string('a', 252)}.pdf", "application/pdf", "%PDF-1.7");

        using var response = await factory.CreateClient().PostAsync("/api/documents", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task MismatchedContentTypeAndInvalidSignatureReturn415()
    {
        await factory.ResetAsync();
        using var mismatch = CreateForm("offre.pdf", "application/zip", "%PDF-1.7");
        using var mismatchResponse = await factory.CreateClient().PostAsync("/api/documents", mismatch);
        using var invalidSignature = CreateForm("offre.pdf", "application/pdf", "not a PDF");
        using var invalidSignatureResponse = await factory.CreateClient().PostAsync("/api/documents", invalidSignature);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, mismatchResponse.StatusCode);
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, invalidSignatureResponse.StatusCode);
    }

    [Fact]
    public async Task InvalidDocxPackagesReturn415()
    {
        await factory.ResetAsync();
        using var missingDocument = CreateForm(
            "offre.docx",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            CreateZip("other.xml"));
        using var missingDocumentResponse = await factory.CreateClient().PostAsync("/api/documents", missingDocument);
        using var corrupt = CreateForm(
            "offre.docx",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            "PK\u0003\u0004corrupt");
        using var corruptResponse = await factory.CreateClient().PostAsync("/api/documents", corrupt);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, missingDocumentResponse.StatusCode);
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, corruptResponse.StatusCode);
    }

    [Fact]
    public async Task ValidDocxPackageIsAccepted()
    {
        await factory.ResetAsync();
        using var form = CreateForm(
            "offre.docx",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            CreateZip("word/document.xml"));

        using var response = await factory.CreateClient().PostAsync("/api/documents", form);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task UnavailableStorageReturns503ForUploadAndDownload()
    {
        await factory.ResetAsync();
        var document = new StoredDocument(
            Guid.NewGuid(),
            "offre.pdf",
            "application/pdf",
            1,
            new string('a', 64),
            DateTimeOffset.UtcNow);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<RiposteDbContext>();
            dbContext.Set<StoredDocument>().Add(document);
            await dbContext.SaveChangesAsync();
        }

        factory.ObjectStorage.IsAvailable = false;
        using var form = CreateForm("offre.pdf", "application/pdf", "%PDF-1.7");
        using var upload = await factory.CreateClient().PostAsync("/api/documents", form);
        using var download = await factory.CreateClient().GetAsync($"/api/documents/{document.Id}/content");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, upload.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, download.StatusCode);
    }

    [Fact]
    public async Task UnknownDocumentsReturn404()
    {
        await factory.ResetAsync();
        var unknownId = Guid.NewGuid();
        var client = factory.CreateClient();
        using var metadata = await client.GetAsync($"/api/documents/{unknownId}");
        using var content = await client.GetAsync($"/api/documents/{unknownId}/content");

        Assert.Equal(HttpStatusCode.NotFound, metadata.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, content.StatusCode);
    }

    [Fact]
    public async Task DocumentsCanBeSortedByAnAllowedField()
    {
        await factory.ResetAsync();
        var client = factory.CreateClient();
        using var first = CreateForm("z.pdf", "application/pdf", "%PDF-1.7");
        using var second = CreateForm("a.pdf", "application/pdf", "%PDF-1.7");
        await client.PostAsync("/api/documents", first);
        await client.PostAsync("/api/documents", second);

        var list = await client.GetFromJsonAsync<DocumentListResponse>("/api/documents?orderBy=originalFileName");

        Assert.Equal("a.pdf", list!.Items[0].OriginalFileName);
    }

    private static MultipartFormDataContent CreateForm(string name, string contentType, string content) =>
        CreateForm(name, contentType, System.Text.Encoding.UTF8.GetBytes(content));

    private static MultipartFormDataContent CreateForm(string name, string contentType, byte[] content)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(file, "file", name);
        return form;
    }

    private static byte[] CreateZip(string entryName)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            archive.CreateEntry(entryName);
        }

        return stream.ToArray();
    }
}
