using System.Net.Http.Headers;
using System.Text.Json;

namespace RiposteOS.Infrastructure.Documents;

public sealed class DoclingDocumentParser(HttpClient client) : IDocumentParser
{
    public async Task<ParsedDocument> ParseAsync(
        string fileName,
        string contentType,
        Stream content,
        CancellationToken cancellationToken)
    {
        using var form = new MultipartFormDataContent();
        using var file = new StreamContent(content);
        file.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        form.Add(file, "files", fileName);
        form.Add(new StringContent("json"), "to_formats");
        using var response = await client.PostAsync("v1/convert/file", form, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var payload = await JsonDocument.ParseAsync(responseStream, cancellationToken: cancellationToken);
        if (!payload.RootElement.TryGetProperty("status", out var status)
            || status.GetString() is not ("success" or "partial_success")
            || !payload.RootElement.TryGetProperty("document", out var document)
            || !document.TryGetProperty("json_content", out var jsonContent))
        {
            throw new InvalidOperationException("Docling did not return a usable document.");
        }

        return Parse(jsonContent);
    }

    private static ParsedDocument Parse(JsonElement document)
    {
        var passages = new List<ParsedPassage>();
        var pageCount = document.GetProperty("pages").EnumerateObject().Count();
        var texts = document.GetProperty("texts");

        string? sectionTitle = null;
        foreach (var item in texts.EnumerateArray())
        {
            var value = item.GetProperty("text").GetString()!.Trim();

            if (IsHeading(item))
            {
                sectionTitle = value;
                continue;
            }

            passages.Add(new ParsedPassage(
                value,
                GetPageNumber(item),
                sectionTitle,
                null));
        }

        foreach (var table in document.GetProperty("tables").EnumerateArray())
        {
            var tableText = string.Join('\n', table.GetProperty("data").GetProperty("table_cells").EnumerateArray()
                .Select(cell => cell.GetProperty("text").GetString()!.Trim()));
            passages.Add(new ParsedPassage(tableText, GetPageNumber(table), null, null));
        }

        return new ParsedDocument(pageCount, passages);
    }

    private static bool IsHeading(JsonElement element) =>
        element.TryGetProperty("label", out var label)
        && label.GetString() is "section_header" or "title";

    private static int? GetPageNumber(JsonElement element)
    {
        return element.GetProperty("prov")[0].GetProperty("page_no").GetInt32();
    }

}
