using System.Net;
using System.Text;
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
