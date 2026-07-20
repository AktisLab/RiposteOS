using System.Net;
using System.Text;
using System.IO.Compression;
using RiposteOS.Infrastructure.Documents;

namespace RiposteOS.Tests.Documents;

public sealed class DoclingDocumentParserTests
{
    [Fact]
    public async Task SendsMultipartJsonRequestAndKeepsDoclingProvenance()
    {
        var handler = new StubHandler("""
            {"status":"success","document":{"json_content":{"pages":{"1":{}},"texts":[{"label":"section_header","text":"Présentation","prov":[{"page_no":1}]},{"label":"text","text":" Passage PDF ","prov":[{"page_no":1}]}],"tables":[{"prov":[{"page_no":2}],"data":{"table_cells":[{"text":"Cellule A"},{"text":"Cellule B"}]}}]}}}
            """);
        var parser = new DoclingDocumentParser(new HttpClient(handler) { BaseAddress = new Uri("http://docling/") });

        var result = await parser.ParseAsync(
            "offre.pdf",
            "application/pdf",
            new MemoryStream([1, 2]),
            CancellationToken.None);

        Assert.Equal("/v1/convert/file", handler.Request!.RequestUri!.AbsolutePath);
        Assert.Contains("to_formats", handler.Body, StringComparison.Ordinal);
        Assert.Equal(1, result.PageCount);
        Assert.Equal(2, result.Passages.Count);
        Assert.Equal("Passage PDF", result.Passages[0].Text);
        Assert.Equal(1, result.Passages[0].PageNumber);
        Assert.Equal("Présentation", result.Passages[0].SectionTitle);
        Assert.Equal("Cellule A\nCellule B", result.Passages[1].Text);
        Assert.Equal(2, result.Passages[1].PageNumber);
    }

    [Fact]
    public async Task RejectsFailedDoclingResponse()
    {
        var parser = new DoclingDocumentParser(new HttpClient(new StubHandler("{\"status\":\"failure\"}"))
        {
            BaseAddress = new Uri("http://docling/"),
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() => parser.ParseAsync(
            "offre.pdf", "application/pdf", new MemoryStream([1]), CancellationToken.None));
    }

    [Fact]
    public async Task NormalizesWindowsOoxmlEntryNamesBeforeSendingToDocling()
    {
        var handler = new StubHandler("""
            {"status":"success","document":{"json_content":{"pages":{},"texts":[],"tables":[]}}}
            """);
        var parser = new DoclingDocumentParser(new HttpClient(handler) { BaseAddress = new Uri("http://docling/") });

        await parser.ParseAsync(
            "offre.docx",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            new MemoryStream(CreateZip(@"word\document.xml")),
            CancellationToken.None);

        Assert.Contains("word/document.xml", handler.Body, StringComparison.Ordinal);
        Assert.DoesNotContain(@"word\document.xml", handler.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SkipsEmptyPassagesReturnedByDocling()
    {
        var parser = new DoclingDocumentParser(new HttpClient(new StubHandler("""
            {"status":"success","document":{"json_content":{"pages":{},"texts":[{"label":"text","text":"   ","prov":[{"page_no":1}]}],"tables":[{"prov":[{"page_no":1}],"data":{"table_cells":[{"text":" "}]}}]}}}
            """)) { BaseAddress = new Uri("http://docling/") });

        var result = await parser.ParseAsync("offre.pdf", "application/pdf", new MemoryStream([1]), CancellationToken.None);

        Assert.Empty(result.Passages);
    }

    [Fact]
    public async Task AcceptsPassagesWithoutProvenance()
    {
        var parser = new DoclingDocumentParser(new HttpClient(new StubHandler("""
            {"status":"success","document":{"json_content":{"pages":{},"texts":[{"label":"text","text":"Sans page","prov":[]}],"tables":[]}}}
            """)) { BaseAddress = new Uri("http://docling/") });

        var result = await parser.ParseAsync("offre.pdf", "application/pdf", new MemoryStream([1]), CancellationToken.None);

        Assert.Null(Assert.Single(result.Passages).PageNumber);
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

    private sealed class StubHandler(string response) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        public string Body { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            Body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(response, Encoding.UTF8, "application/json"),
            };
        }
    }
}
