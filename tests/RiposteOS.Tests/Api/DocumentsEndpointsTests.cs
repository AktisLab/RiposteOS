using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
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
}
