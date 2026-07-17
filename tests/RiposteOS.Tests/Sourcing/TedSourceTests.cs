using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RiposteOS.Core.Sourcing;
using RiposteOS.Infrastructure.Sourcing;

namespace RiposteOS.Tests.Sourcing;

public sealed class TedSourceTests
{
    [Fact]
    public async Task SearchBuildsAnExpertQueryAndMapsTheOfficialTedShape()
    {
        const string payload = """
            {
              "totalNoticeCount": 1,
              "notices": [{
                "publication-number": "487188-2026",
                "publication-date": "2026-07-15+02:00",
                "notice-title": {
                  "fra": "Développement d'un logiciel métier",
                  "eng": "Business software development"
                },
                "buyer-name": { "fra": ["Métropole de Lyon"] },
                "description-proc": { "fra": ["Conception, réalisation et maintenance de la solution."] },
                "procedure-type": "open",
                "contract-nature": "services",
                "estimated-value-proc": "1250000.50",
                "estimated-value-cur-proc": "EUR",
                "duration-period-value-lot": "36",
                "duration-period-unit-lot": "MONTH",
                "document-url-lot": "https://marches.example/dce",
                "classification-cpv": ["72262000", "72262000", "72200000"],
                "deadline-receipt-tender-date-lot": ["2026-08-01+02:00", "2026-07-31+02:00"],
                "deadline-receipt-tender-time-lot": ["12:00:00+02:00", "10:30:00+02:00"],
                "place-of-performance-country-lot": "FRA",
                "place-of-performance-post-code-lot": ["69001", "97100", "inconnu"],
                "links": {
                  "html": {
                    "FRA": "https://ted.europa.eu/fr/notice/-/detail/487188-2026",
                    "ENG": "https://ted.europa.eu/en/notice/-/detail/487188-2026"
                  }
                }
              }]
            }
            """;
        var handler = new StubHttpMessageHandler(payload);
        var source = CreateSource(handler);

        var page = await source.SearchPageAsync(
            [" logiciel métier ", "LOGICIEL MÉTIER", "api"],
            ["maintenance", "maintenance"],
            ["72", "48000000"],
            ["FRA", "BEL"],
            new DateOnly(2026, 7, 15),
            0,
            500,
            CancellationToken.None);

        var opportunity = Assert.Single(page.Opportunities);
        Assert.Equal(1, page.TotalCount);
        Assert.Equal(1, page.Fetched);
        Assert.Equal(0, page.Skipped);
        Assert.Equal("487188-2026", opportunity.SourceId);
        Assert.Equal("Développement d'un logiciel métier", opportunity.Title);
        Assert.Equal("Métropole de Lyon", opportunity.Buyer);
        Assert.Equal(new DateOnly(2026, 7, 15), opportunity.PublicationDate);
        Assert.Equal(new DateTimeOffset(2026, 7, 31, 8, 30, 0, TimeSpan.Zero), opportunity.ResponseDeadline);
        Assert.Equal(["FRA"], opportunity.CountryCodes);
        Assert.Equal(["69", "971"], opportunity.DepartmentCodes);
        Assert.Equal(["72262000", "72200000"], opportunity.CpvCodes);
        Assert.Equal("Conception, réalisation et maintenance de la solution.", opportunity.Description);
        Assert.Equal("open", opportunity.ProcedureType);
        Assert.Equal("services", opportunity.ContractNature);
        Assert.Equal(1_250_000.50m, opportunity.EstimatedValue);
        Assert.Equal("EUR", opportunity.Currency);
        Assert.Equal("36 MONTH", opportunity.ExecutionDuration);
        Assert.Equal("https://marches.example/dce", opportunity.DocumentUrl);
        Assert.Equal("https://ted.europa.eu/fr/notice/-/detail/487188-2026", opportunity.NoticeUrl);
        Assert.Equal(new Uri("https://ted.example/v3/notices/search"), handler.RequestUri);

        using var request = JsonDocument.Parse(handler.RequestBody!);
        var root = request.RootElement;
        var query = root.GetProperty("query").GetString();
        Assert.Contains("publication-date=20260715", query);
        Assert.Contains("notice-type IN (pin-cfc-standard", query);
        Assert.Contains("(notice-title IN (\"logiciel métier\" \"api\") OR classification-cpv IN (72* 48000000))", query);
        Assert.Contains("notice-title NOT IN (\"maintenance\")", query);
        Assert.Contains("place-of-performance-country-lot IN (FRA BEL)", query);
        Assert.Equal(1, root.GetProperty("page").GetInt32());
        Assert.Equal(100, root.GetProperty("limit").GetInt32());
        Assert.Equal("PAGE_NUMBER", root.GetProperty("paginationMode").GetString());
        Assert.True(root.GetProperty("onlyLatestVersions").GetBoolean());
        var requestedFields = root.GetProperty("fields")
            .EnumerateArray()
            .Select(field => field.GetString())
            .ToArray();
        Assert.Contains("description-proc", requestedFields);
        Assert.Contains("estimated-value-proc", requestedFields);
        Assert.Contains("document-url-lot", requestedFields);
    }

    [Fact]
    public async Task MultiLotValuesAreNotPresentedAsProcedureTotals()
    {
        const string payload = """
            {
              "totalNoticeCount": 1,
              "notices": [{
                "publication-number": "487194-2026",
                "publication-date": "2026-07-15",
                "notice-title": { "fra": "Accord-cadre multi-lots" },
                "estimated-value-lot": ["100", "200"],
                "estimated-value-cur-lot": ["EUR", "EUR"],
                "duration-period-value-lot": ["12", "24"],
                "duration-period-unit-lot": ["MONTH", "MONTH"]
              }]
            }
            """;
        var source = CreateSource(new StubHttpMessageHandler(payload));

        var page = await source.SearchPageAsync(
            ["accord-cadre"], [], [], [], new DateOnly(2026, 7, 15), 1, 10, CancellationToken.None);

        var opportunity = Assert.Single(page.Opportunities);
        Assert.Null(opportunity.EstimatedValue);
        Assert.Null(opportunity.Currency);
        Assert.Null(opportunity.ExecutionDuration);
    }

    [Fact]
    public async Task SingleLotFallbacksRemainUsefulWithoutInventingMultiLotData()
    {
        const string payload = """
            {
              "totalNoticeCount": 3,
              "notices": [
                {
                  "publication-number": "487195-2026",
                  "publication-date": "2026-07-15",
                  "notice-title": { "deu": "Ein Los" },
                  "description-lot": { "fra": ["Description du lot unique"] },
                  "estimated-value-lot": "250000",
                  "estimated-value-cur-lot": "GBP",
                  "duration-period-value-lot": "6",
                  "document-restricted-url-lot": "https://ted.example/documents"
                },
                {
                  "publication-number": "487196-2026",
                  "publication-date": "2026-07-15",
                  "notice-title": { "fra": "Plusieurs lots" },
                  "description-lot": { "fra": ["Lot un", "Lot deux"] },
                  "estimated-value-proc": "inconnu",
                  "estimated-value-cur-lot": ["EUR", "USD"],
                  "duration-period-value-lot": "9",
                  "duration-period-unit-lot": ["MONTH", "DAY"]
                },
                {
                  "publication-number": "487197-2026",
                  "publication-date": "2026-07-15",
                  "notice-title": { "fra": "Langue de repli" },
                  "description-lot": { "deu": "Beschreibung" }
                }
              ]
            }
            """;
        var source = CreateSource(new StubHttpMessageHandler(payload));

        var page = await source.SearchPageAsync(
            ["lot"], [], [], [], new DateOnly(2026, 7, 15), 1, 10, CancellationToken.None);

        Assert.Collection(
            page.Opportunities,
            opportunity =>
            {
                Assert.Equal("Ein Los", opportunity.Title);
                Assert.Equal("Description du lot unique", opportunity.Description);
                Assert.Equal(250_000m, opportunity.EstimatedValue);
                Assert.Equal("GBP", opportunity.Currency);
                Assert.Equal("6", opportunity.ExecutionDuration);
                Assert.Equal("https://ted.example/documents", opportunity.DocumentUrl);
            },
            opportunity =>
            {
                Assert.Null(opportunity.Description);
                Assert.Null(opportunity.EstimatedValue);
                Assert.Null(opportunity.Currency);
                Assert.Equal("9", opportunity.ExecutionDuration);
            },
            opportunity => Assert.Equal("Beschreibung", opportunity.Description));
    }

    [Fact]
    public async Task EmptyKeywordsDoNotCallTed()
    {
        var handler = new StubHttpMessageHandler("{}");
        var source = CreateSource(handler);

        var page = await source.SearchPageAsync(
            [" "],
            [],
            [],
            [],
            new DateOnly(2026, 7, 15),
            1,
            100,
            CancellationToken.None);

        Assert.Equal(0, page.Fetched);
        Assert.Null(handler.RequestUri);
    }

    [Fact]
    public async Task ReadPagesUsesTheSourceCursorAndFollowsPagination()
    {
        var handler = new SequenceHttpMessageHandler(
            Page("487188-2026", totalCount: 2),
            Page("487189-2026", totalCount: 2));
        var options = new TedOptions { InitialLookbackDays = 30, OverlapDays = 2 };
        var source = new TedSource(
            new HttpClient(handler) { BaseAddress = new Uri("https://ted.example/") },
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
        Assert.Equal(
            ["487188-2026", "487189-2026"],
            pages.SelectMany(page => page.Opportunities).Select(item => item.SourceId));
        Assert.Contains("classification-cpv IN (72* 48000000)", GetQuery(handler.RequestBodies[0]));
        Assert.Equal(1, GetPageNumber(handler.RequestBodies[0]));
        Assert.Equal(2, GetPageNumber(handler.RequestBodies[1]));
    }

    [Fact]
    public async Task ReadPagesStopsAfterAnEmptyResponse()
    {
        var handler = new SequenceHttpMessageHandler("""
            { "totalNoticeCount": 5, "notices": [] }
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
        Assert.Single(handler.RequestBodies);
    }

    [Fact]
    public async Task MappingFallsBackToAvailableLanguagesAndSkipsMalformedNotices()
    {
        const string payload = """
            {
              "totalNoticeCount": 3,
              "notices": [
                {
                  "publication-date": "2026-07-15+02:00",
                  "notice-title": { "eng": "Missing identifier" }
                },
                {
                  "publication-number": "bad-date",
                  "publication-date": "invalid",
                  "notice-title": { "eng": "Invalid publication date" }
                },
                {
                  "publication-number": "487190-2026",
                  "publication-date": "2026-07-15+02:00",
                  "notice-title": { "deu": "Softwareentwicklung" },
                  "buyer-name": { "deu": ["Stadt Köln"] },
                  "deadline-receipt-tender-date-lot": ["2026-08-01+02:00"],
                  "classification-cpv": "72262000",
                  "place-of-performance-country-lot": "DEU",
                  "place-of-performance-post-code-lot": "50667",
                  "links": { "html": { "DEU": "https://ted.example/de/487190-2026" } }
                }
              ]
            }
            """;
        var source = CreateSource(new StubHttpMessageHandler(payload));

        var page = await source.SearchPageAsync(
            ["a\\\"b"],
            [],
            [],
            [],
            new DateOnly(2026, 7, 15),
            -1,
            10,
            CancellationToken.None);

        var opportunity = Assert.Single(page.Opportunities);
        Assert.Equal(2, page.Skipped);
        Assert.Collection(
            page.Issues!,
            issue =>
            {
                Assert.Null(issue.SourceId);
                Assert.Equal("mapping_json", issue.ErrorCode);
            },
            issue =>
            {
                Assert.Equal("bad-date", issue.SourceId);
                Assert.Equal("mapping_format", issue.ErrorCode);
                Assert.Contains("Invalid publication date", issue.RawPayload, StringComparison.Ordinal);
            });
        Assert.Equal("Softwareentwicklung", opportunity.Title);
        Assert.Equal("Stadt Köln", opportunity.Buyer);
        Assert.Equal(["DEU"], opportunity.CountryCodes);
        Assert.Empty(opportunity.DepartmentCodes);
        Assert.Equal(["72262000"], opportunity.CpvCodes);
        Assert.Equal(new DateTimeOffset(2026, 8, 1, 21, 59, 59, TimeSpan.Zero), opportunity.ResponseDeadline);
        Assert.Equal("https://ted.example/de/487190-2026", opportunity.NoticeUrl);
    }

    [Fact]
    public async Task MappingHandlesSparseAndInternationalNotices()
    {
        const string payload = """
            {
              "totalNoticeCount": 4,
              "notices": [
                {
                  "publication-number": "487191-2026",
                  "publication-date": "2026-07-15",
                  "notice-title": { "eng": "Software platform" },
                  "deadline-receipt-tender-date-lot": ["bad", "2026-08-01"],
                  "deadline-receipt-tender-time-lot": ["bad", "10:00:00Z"],
                  "classification-cpv": {},
                  "place-of-performance-country-lot": ["FRA", "BEL"],
                  "place-of-performance-post-code-lot": ["97", "--"],
                  "links": { "html": { "ENG": "https://ted.example/en/487191-2026" } }
                },
                {
                  "publication-number": "487192-2026",
                  "publication-date": "2026-07-15",
                  "notice-title": { "fra": "Plateforme logicielle" },
                  "buyer-name": 42,
                  "deadline-receipt-tender-date-lot": ["2026-08-02-04:00"],
                  "deadline-receipt-tender-time-lot": ["11:00:00-04:00"],
                  "links": { "html": [] }
                },
                {
                  "publication-number": 42,
                  "publication-date": "2026-07-15",
                  "notice-title": { "eng": "Invalid identifier" }
                },
                {
                  "publication-number": "487193-2026",
                  "publication-date": "2026-07-15",
                  "notice-title": { "eng": 42 }
                }
              ]
            }
            """;
        var source = CreateSource(new StubHttpMessageHandler(payload));

        var page = await source.SearchPageAsync(
            ["logiciel"],
            [],
            [],
            [],
            new DateOnly(2026, 7, 15),
            1,
            10,
            CancellationToken.None);

        Assert.Equal(2, page.Skipped);
        Assert.Collection(
            page.Opportunities,
            opportunity =>
            {
                Assert.Equal("Software platform", opportunity.Title);
                Assert.Equal("Acheteur non renseigné", opportunity.Buyer);
                Assert.Empty(opportunity.DepartmentCodes);
                Assert.Empty(opportunity.CpvCodes);
                Assert.Equal(new DateTimeOffset(2026, 8, 1, 10, 0, 0, TimeSpan.Zero), opportunity.ResponseDeadline);
                Assert.Equal("https://ted.example/en/487191-2026", opportunity.NoticeUrl);
            },
            opportunity =>
            {
                Assert.Equal("Acheteur non renseigné", opportunity.Buyer);
                Assert.Equal(new DateTimeOffset(2026, 8, 2, 15, 0, 0, TimeSpan.Zero), opportunity.ResponseDeadline);
                Assert.Equal(string.Empty, opportunity.NoticeUrl);
            });
    }

    [Fact]
    public async Task HttpErrorsArePropagated()
    {
        var source = CreateSource(new StubHttpMessageHandler("{}", HttpStatusCode.ServiceUnavailable));

        await Assert.ThrowsAsync<HttpRequestException>(() => source.SearchPageAsync(
            ["logiciel"],
            [],
            [],
            [],
            new DateOnly(2026, 7, 15),
            1,
            10,
            CancellationToken.None));
    }

    private static TedSource CreateSource(HttpMessageHandler handler) => new(
        new HttpClient(handler) { BaseAddress = new Uri("https://ted.example/") },
        Options.Create(new TedOptions()));

    private static int GetPageNumber(string body)
    {
        using var document = JsonDocument.Parse(body);
        return document.RootElement.GetProperty("page").GetInt32();
    }

    private static string GetQuery(string body)
    {
        using var document = JsonDocument.Parse(body);
        return document.RootElement.GetProperty("query").GetString()!;
    }

    private static string Page(string id, int totalCount) => $$"""
        {
          "totalNoticeCount": {{totalCount}},
          "notices": [{
            "publication-number": "{{id}}",
            "publication-date": "2026-07-15+02:00",
            "notice-title": { "fra": "Développement logiciel" }
          }]
        }
        """;

    private sealed class StubHttpMessageHandler(
        string payload,
        HttpStatusCode statusCode = HttpStatusCode.OK) : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }

        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            RequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json"),
            };
        }
    }

    private sealed class SequenceHttpMessageHandler(params string[] payloads) : HttpMessageHandler
    {
        private int _index;

        public List<string> RequestBodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestBodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(payloads[_index++], Encoding.UTF8, "application/json"),
            };
        }
    }
}
