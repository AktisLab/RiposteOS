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
            ["72", "48000000"],
            new DateOnly(2026, 6, 18),
            25,
            25,
            CancellationToken.None);

        var opportunity = Assert.Single(page.Opportunities);
        Assert.Equal(1, page.TotalCount);
        Assert.Equal(1, page.Fetched);
        Assert.Equal(0, page.Skipped);
        Assert.Equal("26-59690", opportunity.SourceId);
        Assert.Equal(["FRA"], opportunity.CountryCodes);
        Assert.Equal(["69"], opportunity.DepartmentCodes);
        Assert.Equal(["72200000"], opportunity.CpvCodes);
        Assert.Contains("search(objet, \"logiciel métier\")", Uri.UnescapeDataString(handler.RequestUri!.Query));
        Assert.Contains("search(donnees, \"CPV 72\")", Uri.UnescapeDataString(handler.RequestUri.Query));
        Assert.Contains("search(donnees, \"CPV 48000000\")", Uri.UnescapeDataString(handler.RequestUri.Query));
        Assert.Contains("not (search(objet, \"porte automatique\"))", Uri.UnescapeDataString(handler.RequestUri.Query));
        Assert.Contains("dateparution = date'2026-06-18'", Uri.UnescapeDataString(handler.RequestUri.Query));
        Assert.Contains("nature_libelle in (\"Avis de marché\", \"Rectificatif\")", Uri.UnescapeDataString(handler.RequestUri.Query));
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
                "datelimitereponse": "2026-08-15T12:00:00+02:00",
                "donnees": "{\"cac:MainCommodityClassification\":{\"cbc:ItemClassificationCode\":{\"@listName\":\"cpv\",\"#text\":\"72262000\"}}}"
              }]
            }
            """;
        var source = CreateSource(new StubHttpMessageHandler(payload));

        var page = await source.SearchPageAsync(
            ["développement"],
            [],
            [],
            new DateOnly(2026, 7, 15),
            0,
            100,
            CancellationToken.None);

        Assert.Equal(["72262000"], Assert.Single(page.Opportunities).CpvCodes);
    }

    [Fact]
    public async Task SearchFallsBackToTheEformsParticipationRequestDeadline()
    {
        const string payload = """
            {
              "total_count": 1,
              "results": [{
                "idweb": "26-participation-deadline",
                "objet": "Développement applicatif",
                "dateparution": "2026-07-15",
                "nature_libelle": "Avis de marché",
                "donnees": "{\"EFORMS\":{\"ContractNotice\":{\"cac:ProcurementProjectLot\":{\"cac:TenderingProcess\":{\"cac:ParticipationRequestReceptionPeriod\":{\"cbc:EndDate\":\"2026-08-04+02:00\",\"cbc:EndTime\":\"12:00:00.000+02:00\"}}}}}}"
              }]
            }
            """;
        var source = CreateSource(new StubHttpMessageHandler(payload));

        var page = await source.SearchPageAsync(
            ["développement"],
            [],
            [],
            new DateOnly(2026, 7, 15),
            0,
            100,
            CancellationToken.None);

        var opportunity = Assert.Single(page.Opportunities);
        Assert.Equal(new DateTimeOffset(2026, 8, 4, 10, 0, 0, TimeSpan.Zero), opportunity.ResponseDeadline);
        Assert.Equal(0, page.Skipped);
    }

    [Fact]
    public async Task SearchPrefersTheFlatDeadlineOverAnEformsFallback()
    {
        const string payload = """
            {
              "total_count": 1,
              "results": [{
                "idweb": "26-flat-deadline",
                "objet": "Développement applicatif",
                "dateparution": "2026-07-15",
                "datelimitereponse": "2026-08-12T09:30:00+02:00",
                "nature_libelle": "Avis de marché",
                "donnees": "{\"EFORMS\":{\"ContractNotice\":{\"cac:ProcurementProjectLot\":{\"cac:TenderingProcess\":{\"cac:ParticipationRequestReceptionPeriod\":{\"cbc:EndDate\":\"2026-08-04+02:00\",\"cbc:EndTime\":\"12:00:00.000+02:00\"}}}}}}"
              }]
            }
            """;
        var source = CreateSource(new StubHttpMessageHandler(payload));

        var page = await source.SearchPageAsync(
            ["développement"],
            [],
            [],
            new DateOnly(2026, 7, 15),
            0,
            100,
            CancellationToken.None);

        Assert.Equal(
            new DateTimeOffset(2026, 8, 12, 7, 30, 0, TimeSpan.Zero),
            Assert.Single(page.Opportunities).ResponseDeadline);
    }

    [Fact]
    public async Task SearchDoesNotUseTheAdditionalInformationPeriodAsAResponseDeadline()
    {
        const string payload = """
            {
              "total_count": 1,
              "results": [{
                "idweb": "26-information-deadline",
                "objet": "Développement applicatif",
                "dateparution": "2026-07-15",
                "nature_libelle": "Avis de marché",
                "donnees": "{\"EFORMS\":{\"ContractNotice\":{\"cac:ProcurementProjectLot\":{\"cac:TenderingProcess\":{\"cac:AdditionalInformationRequestPeriod\":{\"cbc:EndDate\":\"2026-07-24+02:00\",\"cbc:EndTime\":\"12:00:00.000+02:00\"}}}}}}"
              }]
            }
            """;
        var source = CreateSource(new StubHttpMessageHandler(payload));

        var page = await source.SearchPageAsync(
            ["développement"],
            [],
            [],
            new DateOnly(2026, 7, 15),
            0,
            100,
            CancellationToken.None);

        Assert.Empty(page.Opportunities);
        Assert.Equal(1, page.Skipped);
        var issue = Assert.Single(page.Issues!);
        Assert.Equal("26-information-deadline", issue.SourceId);
        Assert.Equal("mapping_format", issue.ErrorCode);
    }

    [Fact]
    public async Task SearchMapsLegacyRectificationsAndIncompleteEformsPeriodsSafely()
    {
        const string payload = """
            {
              "total_count": 6,
              "results": [
                {
                  "idweb": "26-legacy-rectification",
                  "objet": "Date rectifiée",
                  "dateparution": "2026-07-15",
                  "nature_libelle": "Rectificatif",
                  "donnees": "{\"MAPA\":{\"rectificatif\":{\"lireDate\":\"2026-12-03T11:00:00\"}}}"
                },
                {
                  "idweb": "26-zulu-rectification",
                  "objet": "Date rectifiée UTC",
                  "dateparution": "2026-07-15",
                  "nature_libelle": "Rectificatif",
                  "donnees": "{\"MAPA\":{\"rectificatif\":{\"lireDate\":\"2026-08-10T11:00:00Z\"}}}"
                },
                {
                  "idweb": "26-multiple-periods",
                  "objet": "Plusieurs lots",
                  "dateparution": "2026-07-15",
                  "nature_libelle": "Avis de marché",
                  "donnees": "{\"lots\":[{\"cac:TenderSubmissionDeadlinePeriod\":{\"cbc:EndDate\":\"bad\"}},{\"cac:TenderSubmissionDeadlinePeriod\":{\"cbc:EndDate\":\"2026-12-10\"}}]}"
                },
                {
                  "idweb": "26-no-payload",
                  "objet": "Sans données",
                  "dateparution": "2026-07-15",
                  "nature_libelle": "Avis de marché"
                },
                {
                  "idweb": "26-empty-rectification",
                  "objet": "Date rectifiée vide",
                  "dateparution": "2026-07-15",
                  "nature_libelle": "Rectificatif",
                  "donnees": "{\"lireDate\":\"\"}"
                },
                {
                  "idweb": "26-invalid-rectification",
                  "objet": "Date rectifiée invalide",
                  "dateparution": "2026-07-15",
                  "nature_libelle": "Rectificatif",
                  "donnees": "{\"lireDate\":\"invalid\"}"
                }
              ]
            }
            """;
        var source = CreateSource(new StubHttpMessageHandler(payload));

        var page = await source.SearchPageAsync(
            ["développement"], [], [], new DateOnly(2026, 7, 15), 0, 100, CancellationToken.None);

        Assert.Collection(
            page.Opportunities,
            opportunity => Assert.Equal(
                new DateTimeOffset(2026, 12, 3, 10, 0, 0, TimeSpan.Zero),
                opportunity.ResponseDeadline),
            opportunity => Assert.Equal(
                new DateTimeOffset(2026, 8, 10, 11, 0, 0, TimeSpan.Zero),
                opportunity.ResponseDeadline),
            opportunity => Assert.Equal(
                new DateTimeOffset(2026, 12, 10, 22, 59, 59, TimeSpan.Zero),
                opportunity.ResponseDeadline));
        Assert.Equal(3, page.Skipped);
        Assert.All(page.Issues!, issue => Assert.Equal("mapping_format", issue.ErrorCode));
    }

    [Fact]
    public async Task SearchRejectsMalformedStructuredDeadlinesAndSupportsExplicitOffsets()
    {
        const string payload = """
            {
              "total_count": 5,
              "results": [
                {
                  "idweb": "26-non-string-flat-date",
                  "objet": "Date au mauvais format",
                  "dateparution": "2026-07-15",
                  "datelimitereponse": 42,
                  "nature_libelle": "Avis de marché"
                },
                {
                  "idweb": "26-empty-period",
                  "objet": "Période vide",
                  "dateparution": "2026-07-15",
                  "nature_libelle": "Avis de marché",
                  "donnees": "{\"cac:TenderSubmissionDeadlinePeriod\":{}}"
                },
                {
                  "idweb": "26-invalid-period-date",
                  "objet": "Date de période invalide",
                  "dateparution": "2026-07-15",
                  "nature_libelle": "Avis de marché",
                  "donnees": "{\"cac:TenderSubmissionDeadlinePeriod\":{\"cbc:EndDate\":\"2026-99-99\",\"cbc:EndTime\":\"11:00:00Z\"}}"
                },
                {
                  "idweb": "26-short-time",
                  "objet": "Heure incomplète",
                  "dateparution": "2026-07-15",
                  "nature_libelle": "Avis de marché",
                  "donnees": "{\"cac:TenderSubmissionDeadlinePeriod\":{\"cbc:EndDate\":\"2026-12-10\",\"cbc:EndTime\":\"11\"}}"
                },
                {
                  "idweb": "26-negative-offset",
                  "objet": "Décalage explicite",
                  "dateparution": "2026-07-15",
                  "nature_libelle": "Avis de marché",
                  "donnees": "{\"cac:TenderSubmissionDeadlinePeriod\":{\"cbc:EndDate\":\"2026-08-10-04:00\",\"cbc:EndTime\":\"11:00:00-04:00\"}}"
                }
              ]
            }
            """;
        var source = CreateSource(new StubHttpMessageHandler(payload));

        var page = await source.SearchPageAsync(
            ["développement"], [], [], new DateOnly(2026, 7, 15), 0, 100, CancellationToken.None);

        Assert.Collection(
            page.Opportunities,
            opportunity => Assert.Equal(
                new DateTimeOffset(2026, 12, 10, 22, 59, 59, TimeSpan.Zero),
                opportunity.ResponseDeadline),
            opportunity => Assert.Equal(
                new DateTimeOffset(2026, 8, 10, 15, 0, 0, TimeSpan.Zero),
                opportunity.ResponseDeadline));
        Assert.Equal(3, page.Skipped);
    }

    [Fact]
    public async Task SearchMapsEnrichmentFromAllBoampShapes()
    {
        const string payload = """
            {
              "total_count": 3,
              "results": [
                {
                  "idweb": "26-eforms",
                  "objet": "Marché eForms",
                  "dateparution": "2026-07-15",
                  "datelimitereponse": "2026-08-15T12:00:00+02:00",
                  "procedure_libelle": "Procédure Ouverte",
                  "nature_libelle": "Avis de marché",
                  "donnees": "{\"EFORMS\":{\"ContractNotice\":{\"cac:ProcurementProject\":{\"cbc:Description\":{\"#text\":\"Description eForms\"},\"cbc:ProcurementTypeCode\":{\"@listName\":\"contract-nature\",\"#text\":\"services\"},\"cac:RequestedTenderTotal\":{\"cbc:EstimatedOverallContractAmount\":{\"@currencyID\":\"EUR\",\"#text\":\"900000\"}}},\"cac:TenderingProcess\":{\"cbc:ProcedureCode\":{\"@listName\":\"procurement-procedure-type\",\"#text\":\"open\"}},\"cac:ProcurementProjectLot\":[{\"cac:ProcurementProject\":{\"cac:PlannedPeriod\":{\"cbc:DurationMeasure\":{\"@unitCode\":\"MONTH\",\"#text\":\"12\"}}}},{\"cac:ProcurementProject\":{\"cac:PlannedPeriod\":{\"cbc:DurationMeasure\":{\"@unitCode\":\"MONTH\",\"#text\":\"24\"}}}}]}}}"
                },
                {
                  "idweb": "26-fnsimple",
                  "objet": "Marché FNSimple",
                  "dateparution": "2026-07-15",
                  "datelimitereponse": "2026-08-15T12:00:00+02:00",
                  "procedure_libelle": "Procédure Ouverte",
                  "nature_libelle": "Avis de marché",
                  "donnees": "{\"FNSimple\":{\"initial\":{\"natureMarche\":{\"description\":\"Description FNSimple\",\"valeurEstimee\":{\"valeur\":\"160000\",\"@devise\":\"EUR\"},\"dureeMois\":\"48\"},\"communication\":{\"urlDocConsul\":\"https://example.test/fnsimple-dce\"}}}}"
                },
                {
                  "idweb": "26-mapa",
                  "objet": "Marché MAPA",
                  "dateparution": "2026-07-15",
                  "datelimitereponse": "2026-08-15T12:00:00+02:00",
                  "procedure_libelle": "Procédure Adaptée",
                  "nature_libelle": "Rectificatif",
                  "donnees": "{\"MAPA\":{\"rectificatif\":{\"description\":{\"objet\":\"Description MAPA\"},\"duree\":{\"nbMois\":\"18\"},\"organisme\":{\"urlProfilAcheteur\":\"https://example.test/mapa-dce\"}}}}"
                }
              ]
            }
            """;
        var source = CreateSource(new StubHttpMessageHandler(payload));

        var page = await source.SearchPageAsync(
            ["marché"], [], [], new DateOnly(2026, 7, 15), 0, 100, CancellationToken.None);

        Assert.Collection(
            page.Opportunities,
            opportunity =>
            {
                Assert.Equal("Description eForms", opportunity.Description);
                Assert.Equal("open", opportunity.ProcedureType);
                Assert.Equal("services", opportunity.ContractNature);
                Assert.Equal(900_000m, opportunity.EstimatedValue);
                Assert.Equal("EUR", opportunity.Currency);
                Assert.Null(opportunity.ExecutionDuration);
            },
            opportunity =>
            {
                Assert.Equal("Description FNSimple", opportunity.Description);
                Assert.Equal("Procédure Ouverte", opportunity.ProcedureType);
                Assert.Equal("Avis de marché", opportunity.ContractNature);
                Assert.Equal(160_000m, opportunity.EstimatedValue);
                Assert.Equal("EUR", opportunity.Currency);
                Assert.Equal("48 MONTH", opportunity.ExecutionDuration);
                Assert.Equal("https://example.test/fnsimple-dce", opportunity.DocumentUrl);
            },
            opportunity =>
            {
                Assert.Equal("Description MAPA", opportunity.Description);
                Assert.Equal("Procédure Adaptée", opportunity.ProcedureType);
                Assert.Equal("Rectificatif", opportunity.ContractNature);
                Assert.Equal("18 MONTH", opportunity.ExecutionDuration);
                Assert.Equal("https://example.test/mapa-dce", opportunity.DocumentUrl);
            });
    }

    [Fact]
    public async Task SearchMapsLegacyFallbacksAndASingleEformsLot()
    {
        const string payload = """
            {
              "total_count": 3,
              "results": [
                {
                  "idweb": "26-single-lot",
                  "objet": "Lot eForms unique",
                  "dateparution": "2026-07-15",
                  "datelimitereponse": "2026-08-15T12:00:00+02:00",
                  "donnees": "{\"EFORMS\":{\"ContractNotice\":{\"cac:ProcurementProjectLot\":{\"cac:ProcurementProject\":{\"cac:PlannedPeriod\":{\"cbc:DurationMeasure\":{\"#text\":\"7\"}}}}}}}"
                },
                {
                  "idweb": "26-rectificatif",
                  "objet": "Ancien formulaire rectificatif",
                  "dateparution": "2026-07-15",
                  "datelimitereponse": "2026-08-15T12:00:00+02:00",
                  "procedure_libelle": "Procédure négociée",
                  "nature_libelle": "Services",
                  "donnees": "{\"FNSimple\":{\"rectificatif\":{\"natureMarche\":{\"description\":{\"#text\":\"Description rectifiée\"},\"valeurEstimee\":{\"#text\":\"123000\",\"@devise\":\"USD\"}},\"communication\":{\"urlProfilAch\":\"https://example.test/profil\"}}}}"
                },
                {
                  "idweb": "26-attribution",
                  "objet": "MAPA attribué",
                  "dateparution": "2026-07-15",
                  "datelimitereponse": "2026-08-15T12:00:00+02:00",
                  "donnees": "{\"MAPA\":{\"attribution\":{\"descriptionReduite\":{\"objet\":\"Description attribuée\"}},\"initial\":{\"duree\":{\"txtLibre\":\"Jusqu'au 31 décembre\"},\"organisme\":{\"urlProfilAcheteur\":\"https://example.test/marches\"}}}}"
                }
              ]
            }
            """;
        var source = CreateSource(new StubHttpMessageHandler(payload));

        var page = await source.SearchPageAsync(
            ["marché"], [], [], new DateOnly(2026, 7, 15), 0, 100, CancellationToken.None);

        Assert.Collection(
            page.Opportunities,
            opportunity => Assert.Equal("7", opportunity.ExecutionDuration),
            opportunity =>
            {
                Assert.Equal("Description rectifiée", opportunity.Description);
                Assert.Equal(123_000m, opportunity.EstimatedValue);
                Assert.Equal("USD", opportunity.Currency);
                Assert.Equal("https://example.test/profil", opportunity.DocumentUrl);
            },
            opportunity =>
            {
                Assert.Equal("Description attribuée", opportunity.Description);
                Assert.Equal("Jusqu'au 31 décembre", opportunity.ExecutionDuration);
                Assert.Equal("https://example.test/marches", opportunity.DocumentUrl);
            });
    }

    [Fact]
    public async Task EmptyKeywordsDoNotCallBoamp()
    {
        var handler = new StubHttpMessageHandler("{}");
        var source = CreateSource(handler);

        var page = await source.SearchPageAsync(
            [],
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
        Assert.Contains("search(donnees, \"CPV 72\")", Uri.UnescapeDataString(handler.RequestUris[0].Query));
        Assert.Contains("search(donnees, \"CPV 48000000\")", Uri.UnescapeDataString(handler.RequestUris[0].Query));
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
    public async Task ReadPagesSkipsFrenchSourceWhenFranceIsNotAllowed()
    {
        var handler = new StubHttpMessageHandler("{}");
        var source = CreateSource(handler);
        var settings = new SourcingSettings(
            TestSupport.TestSourcingProfiles.Create() with { AllowedCountryCodes = ["BEL"] },
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

        Assert.Empty(pages);
        Assert.Null(handler.RequestUri);
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
                  "datelimitereponse": "2026-08-15T12:00:00+02:00",
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
            [],
            new DateOnly(2026, 7, 15),
            -1,
            500,
            CancellationToken.None);

        var opportunity = Assert.Single(page.Opportunities);
        Assert.Equal(2, page.Skipped);
        Assert.Collection(
            page.Issues!,
            issue =>
            {
                Assert.Null(issue.SourceId);
                Assert.Equal("mapping_json", issue.ErrorCode);
                Assert.Contains("Missing identifier", issue.RawPayload, StringComparison.Ordinal);
            },
            issue =>
            {
                Assert.Equal("bad-date", issue.SourceId);
                Assert.Equal("mapping_format", issue.ErrorCode);
            });
        Assert.Equal("Acheteur non renseigné", opportunity.Buyer);
        Assert.Equal(new DateTimeOffset(2026, 8, 15, 10, 0, 0, TimeSpan.Zero), opportunity.ResponseDeadline);
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
            "dateparution": "2026-07-15",
            "datelimitereponse": "2026-08-15T12:00:00+02:00"
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
