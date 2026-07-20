using System.IO.Compression;
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
        await using var normalizedContent = await NormalizeOoxmlAsync(content, contentType, cancellationToken);
        using var form = new MultipartFormDataContent();
        using var file = new StreamContent(normalizedContent ?? content);
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

            if (!string.IsNullOrWhiteSpace(value))
            {
                passages.Add(new ParsedPassage(
                    value,
                    GetPageNumber(item),
                    sectionTitle,
                    null));
            }
        }

        foreach (var table in document.GetProperty("tables").EnumerateArray())
        {
            var tableText = string.Join('\n', table.GetProperty("data").GetProperty("table_cells").EnumerateArray()
                .Select(cell => cell.GetProperty("text").GetString()!.Trim())
                .Where(text => !string.IsNullOrWhiteSpace(text)));
            if (!string.IsNullOrWhiteSpace(tableText))
            {
                passages.Add(new ParsedPassage(tableText, GetPageNumber(table), null, null));
            }
        }

        return new ParsedDocument(pageCount, passages);
    }

    private static async Task<MemoryStream?> NormalizeOoxmlAsync(
        Stream content,
        string contentType,
        CancellationToken cancellationToken)
    {
        if (contentType is not (
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
            or "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"))
        {
            return null;
        }

        var normalized = new MemoryStream();
        using (var source = new ZipArchive(content, ZipArchiveMode.Read, leaveOpen: true))
        using (var target = new ZipArchive(normalized, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var entry in source.Entries)
            {
                var normalizedEntry = target.CreateEntry(entry.FullName.Replace('\\', '/'));
                await using var sourceContent = entry.Open();
                await using var targetContent = normalizedEntry.Open();
                await sourceContent.CopyToAsync(targetContent, cancellationToken);
            }
        }

        normalized.Position = 0;
        return normalized;
    }

    private static bool IsHeading(JsonElement element) =>
        element.TryGetProperty("label", out var label)
        && label.GetString() is "section_header" or "title";

    private static int? GetPageNumber(JsonElement element)
    {
        if (element.TryGetProperty("prov", out var provenance)
            && provenance.ValueKind == JsonValueKind.Array
            && provenance.GetArrayLength() > 0
            && provenance[0].TryGetProperty("page_no", out var pageNumber)
            && pageNumber.TryGetInt32(out var value))
        {
            return value;
        }

        return null;
    }

}
