using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using RiposteOS.Core.Sourcing;
using RiposteOS.Infrastructure.Sourcing;

namespace RiposteOS.Tests.Sourcing;

public sealed class BoampSourceTests
{
    [Fact]
    public async Task SearchMapsTheOfficialBoampRecordShape()
    {
        const string payload = """
            {
              "total_count": 1,
              "results": [{
                "idweb": "26-59690",
                "objet": "Développement d'un logiciel métier",
                "dateparution": "2026-06-18",
                "datelimitereponse": "2026-07-17T14:00:00+00:00",
                "nomacheteur": "Acheteur public",
                "code_departement": ["69"],
                "donnees": "{\"OBJET\":{\"CPV\":[{\"PRINCIPAL\":\"72200000\"}]}}",
                "descripteur_code": ["186"],
                "descripteur_libelle": ["Logiciel"],
                "url_avis": "https://www.boamp.fr/pages/avis/?q=idweb:26-59690"
              }]
            }
            """;
        var handler = new StubHttpMessageHandler(payload);
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://boamp.example/api/"),
        };
        var source = new BoampSource(httpClient, Options.Create(new BoampOptions()));

        var page = await source.SearchPageAsync(
            ["logiciel métier"],
            ["porte automatique"],
            new DateOnly(2026, 6, 18),
            25,
            25,
            CancellationToken.None);

        var opportunity = Assert.Single(page.Opportunities);
        Assert.Equal(1, page.TotalCount);
        Assert.Equal(1, page.Fetched);
        Assert.Equal(0, page.Skipped);
        Assert.Equal("26-59690", opportunity.SourceId);
        Assert.Equal(["69"], opportunity.DepartmentCodes);
        Assert.Equal(["72200000"], opportunity.CpvCodes);
        Assert.Contains("search(objet, \"logiciel métier\")", Uri.UnescapeDataString(handler.RequestUri!.Query));
        Assert.Contains("not (search(objet, \"porte automatique\"))", Uri.UnescapeDataString(handler.RequestUri.Query));
        Assert.Contains("dateparution = date'2026-06-18'", Uri.UnescapeDataString(handler.RequestUri.Query));
        Assert.Contains("limit=25", handler.RequestUri.Query);
        Assert.Contains("offset=25", handler.RequestUri.Query);
    }

    [Fact]
    public async Task SearchMapsCpvCodesFromEformsData()
    {
        const string payload = """
            {
              "total_count": 1,
              "results": [{
                "idweb": "26-eforms",
                "objet": "Développement applicatif",
                "dateparution": "2026-07-15",
                "donnees": "{\"cac:MainCommodityClassification\":{\"cbc:ItemClassificationCode\":{\"@listName\":\"cpv\",\"#text\":\"72262000\"}}}"
              }]
            }
            """;
        var source = CreateSource(new StubHttpMessageHandler(payload));

        var page = await source.SearchPageAsync(
            ["développement"],
            [],
            new DateOnly(2026, 7, 15),
            0,
            100,
            CancellationToken.None);

        Assert.Equal(["72262000"], Assert.Single(page.Opportunities).CpvCodes);
    }

    [Fact]
    public async Task EmptyKeywordsDoNotCallBoamp()
    {
        var handler = new StubHttpMessageHandler("{}");
        var source = CreateSource(handler);

        var page = await source.SearchPageAsync(
            [],
            [],
            new DateOnly(2026, 7, 15),
            0,
            100,
            CancellationToken.None);

        Assert.Equal(0, page.Fetched);
        Assert.Null(handler.RequestUri);
    }

    [Fact]
    public async Task ReadPagesUsesTheSourceCursorAndFollowsPagination()
    {
        var handler = new SequenceHttpMessageHandler(
            Page("26-1", totalCount: 2),
            Page("26-2", totalCount: 2));
        var options = new BoampOptions { InitialLookbackDays = 30, OverlapDays = 2 };
        var source = new BoampSource(
            new HttpClient(handler) { BaseAddress = new Uri("https://boamp.example/api/") },
            Options.Create(options));
        var today = new DateOnly(2026, 7, 15);
        var settings = new SourcingSettings(
            TestSupport.TestSourcingProfiles.Create(pageSize: 1),
            DateTimeOffset.UtcNow);
        var pages = new List<SourcingPage>();

        await foreach (var page in source.ReadPagesAsync(settings, today, today, CancellationToken.None))
        {
            pages.Add(page);
        }

        Assert.Equal(today.AddDays(-30), source.GetStartDate(today, null));
        Assert.Equal(today.AddDays(-2), source.GetStartDate(today, today));
        Assert.Equal(2, pages.Count);
        Assert.Equal(["26-1", "26-2"], pages.SelectMany(page => page.Opportunities).Select(item => item.SourceId));
        Assert.Contains("offset=1", handler.RequestUris[1].Query);
    }

    [Fact]
    public async Task ReadPagesStopsAfterAnEmptyResponse()
    {
        var handler = new SequenceHttpMessageHandler("""
            { "total_count": 5, "results": [] }
            """);
        var source = CreateSource(handler);
        var settings = new SourcingSettings(
            TestSupport.TestSourcingProfiles.Create(pageSize: 10),
            DateTimeOffset.UtcNow);
        var pages = new List<SourcingPage>();

        await foreach (var page in source.ReadPagesAsync(
            settings,
            new DateOnly(2026, 7, 15),
            new DateOnly(2026, 7, 15),
            CancellationToken.None))
        {
            pages.Add(page);
        }

        Assert.Equal(0, Assert.Single(pages).Fetched);
        Assert.Single(handler.RequestUris);
    }

    [Fact]
    public async Task MalformedRecordsAreSkippedWithoutLosingValidRecords()
    {
        const string payload = """
            {
              "total_count": 3,
              "results": [
                { "objet": "Missing identifier", "dateparution": "2026-07-15" },
                { "idweb": "bad-date", "objet": "Bad date", "dateparution": "invalid" },
                {
                  "idweb": "26-valid",
                  "objet": "Valid",
                  "dateparution": "2026-07-15",
                  "datelimitereponse": "invalid",
                  "code_departement_prestation": "69",
                  "descripteur_code": null,
                  "descripteur_libelle": 42
                }
              ]
            }
            """;
        var source = CreateSource(new StubHttpMessageHandler(payload));

        var page = await source.SearchPageAsync(
            ["a\\\"b"],
            [],
            new DateOnly(2026, 7, 15),
            -1,
            500,
            CancellationToken.None);

        var opportunity = Assert.Single(page.Opportunities);
        Assert.Equal(2, page.Skipped);
        Assert.Equal("Acheteur non renseigné", opportunity.Buyer);
        Assert.Null(opportunity.ResponseDeadline);
        Assert.Equal(["69"], opportunity.DepartmentCodes);
        Assert.Empty(opportunity.DescriptorCodes);
        Assert.Empty(opportunity.DescriptorLabels);
        Assert.Equal(string.Empty, opportunity.NoticeUrl);
    }

    [Fact]
    public async Task HttpErrorsArePropagated()
    {
        var handler = new StubHttpMessageHandler("{}", HttpStatusCode.ServiceUnavailable);
        var source = CreateSource(handler);

        await Assert.ThrowsAsync<HttpRequestException>(() => source.SearchPageAsync(
            ["logiciel"],
            [],
            new DateOnly(2026, 7, 15),
            0,
            10,
            CancellationToken.None));
    }

    private static BoampSource CreateSource(HttpMessageHandler handler) => new(
        new HttpClient(handler) { BaseAddress = new Uri("https://boamp.example/api/") },
        Options.Create(new BoampOptions()));

    private static string Page(string id, int totalCount) => $$"""
        {
          "total_count": {{totalCount}},
          "results": [{
            "idweb": "{{id}}",
            "objet": "Logiciel",
            "dateparution": "2026-07-15"
          }]
        }
        """;

    private sealed class StubHttpMessageHandler(
        string payload,
        HttpStatusCode statusCode = HttpStatusCode.OK) : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;

            return Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json"),
            });
        }
    }

    private sealed class SequenceHttpMessageHandler(params string[] payloads) : HttpMessageHandler
    {
        private int _index;

        public List<Uri> RequestUris { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUris.Add(request.RequestUri!);
            var payload = payloads[_index++];
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json"),
            });
        }
    }
}
