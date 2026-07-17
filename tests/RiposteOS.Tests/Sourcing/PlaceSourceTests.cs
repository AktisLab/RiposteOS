using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using RiposteOS.Core.Sourcing;
using RiposteOS.Infrastructure.Sourcing;
using RiposteOS.Tests.TestSupport;

namespace RiposteOS.Tests.Sourcing;

public sealed class PlaceSourceTests
{
    [Fact]
    public async Task ReadPagesKeepsThePradoSessionPaginatesAndMapsDetails()
    {
        var handler = new PlaceSequenceHandler(
            SearchPage("3032554", "f2h", "17", "juil.", "2026", 1, 2),
            DetailPage("72268000", "26-12345"),
            SearchPage("3032555", "f3h", "16", "juil.", "2026", 2, 2),
            DetailPage("48000000", null));
        var source = CreateSource(handler);
        var settings = new SourcingSettings(
            TestSourcingProfiles.Create(keywords: ["logiciel"]) with
            {
                CpvWhitelistPrefixes = [],
                CpvWatchPrefixes = [],
            },
            new DateTimeOffset(2026, 7, 17, 12, 0, 0, TimeSpan.Zero));
        var pages = new List<SourcingPage>();

        await foreach (var page in source.ReadPagesAsync(
                           settings,
                           new DateOnly(2026, 7, 15),
                           new DateOnly(2026, 7, 17),
                           CancellationToken.None))
        {
            pages.Add(page);
        }

        Assert.Equal(2, pages.Count);
        Assert.Equal(2, pages.Sum(page => page.Fetched));
        Assert.Equal(
            ["3032554:f2h", "3032555:f3h"],
            pages.SelectMany(page => page.Opportunities).Select(opportunity => opportunity.SourceId));
        var first = Assert.Single(pages[0].Opportunities);
        Assert.Equal("Développement d’un logiciel métier", first.Title);
        Assert.Equal("Direction des achats", first.Buyer);
        Assert.Equal(["75"], first.DepartmentCodes);
        Assert.Equal(["72268000"], first.CpvCodes);
        Assert.Equal("Description complète du besoin", first.Description);
        Assert.Equal("Appel d'offres ouvert", first.ProcedureType);
        Assert.Equal("Services", first.ContractNature);
        Assert.Equal(new DateTimeOffset(2026, 8, 20, 10, 0, 0, TimeSpan.Zero), first.ResponseDeadline);
        Assert.Equal(
            "https://place.example/index.php?page=Entreprise.EntrepriseDemandeTelechargementDce&id=3032554",
            first.DocumentUrl);
        Assert.Equal(
            new SourceOpportunityReference(SourcingSource.Boamp, "26-12345"),
            Assert.Single(first.References));
        Assert.Equal(HttpMethod.Post, handler.Requests[2].Method);
        Assert.DoesNotContain("login", handler.Requests[2].Uri.AbsolutePath, StringComparison.Ordinal);
        Assert.Contains(
            "PRADO_POSTBACK_TARGET=ctl0%24CONTENU_PAGE%24resultSearch%24PagerTop%24ctl2",
            handler.Requests[2].Body,
            StringComparison.Ordinal);
        Assert.Contains("PRADO_PAGESTATE=state-1", handler.Requests[2].Body, StringComparison.Ordinal);

        var reparsed = source.ParseRawOpportunity(first.RawPayload);
        Assert.Equal(first.SourceId, reparsed.SourceId);
        Assert.Equal(first.ResponseDeadline, reparsed.ResponseDeadline);
        Assert.Equal(first.DepartmentCodes, reparsed.DepartmentCodes);
        Assert.Equal(first.CpvCodes, reparsed.CpvCodes);
        Assert.Equal(first.References, reparsed.References);
    }

    [Fact]
    public async Task ReadPagesAcceptsTheFebruaryAbbreviationUsedByPlace()
    {
        var source = CreateSource(new PlaceSequenceHandler(
            SearchPage("3032556", "f4h", "10", "Fév.", "2026", 1, 1)));
        var settings = CreateSettings("logiciel");
        var pages = new List<SourcingPage>();

        await foreach (var page in source.ReadPagesAsync(
                           settings,
                           new DateOnly(2026, 7, 1),
                           new DateOnly(2026, 7, 17),
                           CancellationToken.None))
        {
            pages.Add(page);
        }

        var parsedPage = Assert.Single(pages);
        Assert.Empty(parsedPage.Opportunities);
        Assert.Equal(0, parsedPage.Fetched);
    }

    [Fact]
    public async Task ReadPagesRequestsTheLargestPageSizeFromTheMainPradoForm()
    {
        var firstPage = SearchPage("3032557", "f5h", "10", "Fév.", "2026", 1, 3, 21);
        var resizedPage = SearchPage("3032557", "f5h", "10", "Fév.", "2026", 1, 1, 21)
            .Replace("<option selected=\"selected\" value=\"10\">", "<option value=\"10\">", StringComparison.Ordinal)
            .Replace("<option value=\"20\">", "<option selected=\"selected\" value=\"20\">", StringComparison.Ordinal);
        var handler = new PlaceSequenceHandler(firstPage, resizedPage);
        var source = CreateSource(handler);

        await foreach (var _ in source.ReadPagesAsync(
                           CreateSettings("logiciel"),
                           new DateOnly(2026, 7, 1),
                           new DateOnly(2026, 7, 17),
                           CancellationToken.None))
        {
        }

        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(HttpMethod.Post, handler.Requests[1].Method);
        Assert.DoesNotContain("login", handler.Requests[1].Uri.AbsolutePath, StringComparison.Ordinal);
        Assert.Contains(
            "PRADO_POSTBACK_TARGET=ctl0%24CONTENU_PAGE%24resultSearch%24listePageSizeTop",
            handler.Requests[1].Body,
            StringComparison.Ordinal);
        Assert.Contains(
            "ctl0%24CONTENU_PAGE%24resultSearch%24listePageSizeTop=20",
            handler.Requests[1].Body,
            StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(PageSizeContractsThatMustNotPost))]
    public async Task ReadPagesDoesNotPostAnInvalidOrAlreadyAppliedPageSizeContract(string searchPage)
    {
        var handler = new PlaceSequenceHandler(searchPage);
        var source = CreateSource(handler);

        await foreach (var _ in source.ReadPagesAsync(
                           CreateSettings("logiciel"),
                           new DateOnly(2026, 7, 1),
                           new DateOnly(2026, 7, 17),
                           CancellationToken.None))
        {
        }

        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task ReadPagesStopsWhenPlaceDoesNotExposeANextPageEventTarget()
    {
        var searchPage = SearchPage("3032558", "f6h", "10", "Fév.", "2026", 1, 2)
            .Replace(
                "<script>new Prado.WebUI.TLinkButton({'ID':\"ctl0_CONTENU_PAGE_resultSearch_PagerTop_ctl2\",'EventTarget':\"ctl0$CONTENU_PAGE$resultSearch$PagerTop$ctl2\"});</script>",
                "<script>new Prado.WebUI.TLinkButton({'ID':\"another-control\",'EventTarget':\"another$target\"});</script>",
                StringComparison.Ordinal);
        var handler = new PlaceSequenceHandler(searchPage);
        var source = CreateSource(handler);

        await foreach (var _ in source.ReadPagesAsync(
                           CreateSettings("logiciel"),
                           new DateOnly(2026, 7, 1),
                           new DateOnly(2026, 7, 17),
                           CancellationToken.None))
        {
        }

        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task ReadPagesUsesKeywordAndCpvUnionWithoutImportingDuplicatesOrExclusions()
    {
        var handler = new PlaceSequenceHandler(
            SearchPage("3032554", "f2h", "17", "juil.", "2026", 1, 1),
            DetailPage("72268000", null),
            SearchPage("3032554", "f2h", "17", "juil.", "2026", 1, 1));
        var source = CreateSource(handler);
        var settings = new SourcingSettings(
            TestSourcingProfiles.Create(keywords: ["logiciel"], excludedKeywords: ["porte automatique"]) with
            {
                CpvWhitelistPrefixes = ["72"],
                CpvWatchPrefixes = [],
            },
            DateTimeOffset.UtcNow);
        var opportunities = new List<SourceOpportunity>();

        await foreach (var page in source.ReadPagesAsync(
                           settings,
                           new DateOnly(2026, 7, 17),
                           new DateOnly(2026, 7, 17),
                           CancellationToken.None))
        {
            opportunities.AddRange(page.Opportunities);
        }

        Assert.Single(opportunities);
        Assert.Equal(3, handler.Requests.Count);
        Assert.Contains("keyWord=logiciel", handler.Requests[0].Uri.Query, StringComparison.Ordinal);
        Assert.Contains("keyWord=72", handler.Requests[2].Uri.Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReadPagesDoesNotCallPlaceWhenFranceIsExcluded()
    {
        var handler = new PlaceSequenceHandler();
        var source = CreateSource(handler);
        var settings = new SourcingSettings(
            TestSourcingProfiles.Create() with { AllowedCountryCodes = ["BEL"] },
            DateTimeOffset.UtcNow);

        await foreach (var _ in source.ReadPagesAsync(
                           settings,
                           new DateOnly(2026, 7, 17),
                           new DateOnly(2026, 7, 17),
                           CancellationToken.None))
        {
        }

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public void CursorUsesInitialLookbackAndOverlap()
    {
        var source = new PlaceSource(
            new HttpClient { BaseAddress = new Uri("https://place.example/") },
            Options.Create(new PlaceOptions { InitialLookbackDays = 20, OverlapDays = 3 }));
        var today = new DateOnly(2026, 7, 17);

        Assert.Equal(today.AddDays(-20), source.GetStartDate(today, null));
        Assert.Equal(today.AddDays(-3), source.GetStartDate(today, today));
    }

    [Fact]
    public async Task SparseResultsUseMachineDatesAndSafeDetailFallbacks()
    {
        const string search = """
            <html><body><form action="/search">
              <input name="PRADO_PAGESTATE" value="state">
              <span id="result_nombreElement">1</span>
              <span id="result_nombrePageTop">1</span>
              <div class="item_consultation">
                <input name="row$refCons" value="3032556">
                <input name="row$orgCons" value="f4h">
                <time datetime="2026-07-17T08:00:00+02:00"></time>
                <div class="objet-line"><span title="Portail usager">Portail</span></div>
                <div class="cons_procedure">Procédure adaptée</div>
                <div class="cons_categorie"><abbr title="Fournitures">F</abbr></div>
              </div>
            </form></body></html>
            """;
        const string detail = """
            <html><body>
              <dl><dt>Entité d’Achat</dt><dd>Service numérique</dd></dl>
              <div><label>Département</label><div>Corse (2A), Guadeloupe (971)</div></div>
              <div><label>Date limite</label><span>date inconnue</span></div>
              <span data-code-cpv="invalide">invalide</span>
              <a href="https://ted.europa.eu/fr/notice/-/detail/123456-2026">TED 123456-2026</a>
              <a href="https://boamp.fr/avis/sans-reference">BOAMP</a>
            </body></html>
            """;
        var handler = new PlaceSequenceHandler(search, detail);
        var source = CreateSource(handler);
        var settings = new SourcingSettings(
            TestSourcingProfiles.Create(keywords: ["portail"]) with
            {
                CpvWhitelistPrefixes = [],
                CpvWatchPrefixes = [],
            },
            DateTimeOffset.UtcNow);
        var opportunities = new List<SourceOpportunity>();

        await foreach (var page in source.ReadPagesAsync(
                           settings,
                           new DateOnly(2026, 7, 17),
                           new DateOnly(2026, 7, 17),
                           CancellationToken.None))
        {
            opportunities.AddRange(page.Opportunities);
        }

        var opportunity = Assert.Single(opportunities);
        Assert.Equal("3032556:f4h", opportunity.SourceId);
        Assert.Equal("Service numérique", opportunity.Buyer);
        Assert.Null(opportunity.ResponseDeadline);
        Assert.Equal(["2A", "971"], opportunity.DepartmentCodes);
        Assert.Empty(opportunity.CpvCodes);
        Assert.Null(opportunity.DocumentUrl);
        Assert.Equal(
            new SourceOpportunityReference(SourcingSource.Ted, "123456-2026"),
            Assert.Single(opportunity.References));
    }

    [Fact]
    public async Task AdvertisedResultsWithoutRowsFailTheRunInsteadOfAdvancingSilently()
    {
        const string changedContract = """
            <html><body>
              <span id="result_nombreElement">12</span>
              <span id="result_nombrePageTop">2</span>
            </body></html>
            """;
        var source = CreateSource(new PlaceSequenceHandler(changedContract));
        var settings = new SourcingSettings(
            TestSourcingProfiles.Create(keywords: ["logiciel"]) with
            {
                CpvWhitelistPrefixes = [],
                CpvWatchPrefixes = [],
            },
            DateTimeOffset.UtcNow);

        await Assert.ThrowsAsync<FormatException>(async () =>
        {
            await foreach (var _ in source.ReadPagesAsync(
                               settings,
                               new DateOnly(2026, 7, 17),
                               new DateOnly(2026, 7, 17),
                               CancellationToken.None))
            {
            }
        });
    }

    [Theory]
    [MemberData(nameof(MalformedSearchRows))]
    public async Task MalformedSearchRowsFailBeforeTheCursorCanAdvance(string row)
    {
        var search = $"""
            <html><body><form>
              <span id="result_nombreElement">1</span>
              <span id="result_nombrePageTop">1</span>
              {row}
            </form></body></html>
            """;
        var source = CreateSource(new PlaceSequenceHandler(search));

        await Assert.ThrowsAsync<FormatException>(async () =>
        {
            await foreach (var _ in source.ReadPagesAsync(
                               CreateSettings("logiciel"),
                               new DateOnly(2026, 7, 17),
                               new DateOnly(2026, 7, 17),
                               CancellationToken.None))
            {
            }
        });
    }

    [Fact]
    public async Task ExcludedAndOutOfWindowRowsDoNotLoadTheirDetails()
    {
        var excluded = SearchPage("3032557", "f5h", "17", "juil.", "2026", 1, 1)
            .Replace("Développement d’un logiciel métier", "Porte automatique", StringComparison.Ordinal);
        var old = SearchPage("3032558", "f6h", "16", "juil.", "2026", 1, 1);
        var handler = new PlaceSequenceHandler(excluded, old);
        var source = CreateSource(handler);
        var settings = new SourcingSettings(
            TestSourcingProfiles.Create(keywords: ["logiciel"], excludedKeywords: ["porte automatique"]) with
            {
                CpvWhitelistPrefixes = ["72"],
                CpvWatchPrefixes = [],
            },
            DateTimeOffset.UtcNow);
        var opportunities = new List<SourceOpportunity>();

        await foreach (var page in source.ReadPagesAsync(
                           settings,
                           new DateOnly(2026, 7, 17),
                           new DateOnly(2026, 7, 17),
                           CancellationToken.None))
        {
            opportunities.AddRange(page.Opportunities);
        }

        Assert.Empty(opportunities);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public void RawIssueEnvelopeCanBeRetriedAfterTheParserIsFixed()
    {
        const string payload = """
            {
              "searchItem": {
                "sourceId": "3032559:f7h",
                "organizationCode": "f7h",
                "title": "Portail métier",
                "buyer": "Acheteur",
                "publicationDate": "2026-07-17",
                "description": null,
                "procedureType": null,
                "contractNature": null,
                "departmentCodes": [],
                "noticeUrl": "https://place.example/app.php/entreprise/consultation/3032559?orgAcronyme=f7h"
              },
              "detailHtml": "<html><body><span data-code-cpv=\"72200000\">72200000</span></body></html>"
            }
            """;
        var source = CreateSource(new PlaceSequenceHandler());

        var opportunity = source.ParseRawOpportunity(payload);

        Assert.Equal("3032559:f7h", opportunity.SourceId);
        Assert.Equal(["72200000"], opportunity.CpvCodes);
    }

    public static TheoryData<string> MalformedSearchRows => new()
    {
        """
        <div class="item_consultation">
          <input name="row$orgCons" value="f8h">
          <div class="objet-line">Logiciel</div>
          <time datetime="2026-07-17"></time>
        </div>
        """,
        """
        <div class="item_consultation">
          <input name="row$refCons" value="3032560">
          <div class="objet-line">Logiciel</div>
          <time datetime="2026-07-17"></time>
        </div>
        """,
        """
        <div class="item_consultation">
          <input name="row$refCons" value="3032561">
          <input name="row$orgCons" value="f9h">
          <time datetime="2026-07-17"></time>
        </div>
        """,
        """
        <div class="item_consultation">
          <input name="row$refCons" value="3032562">
          <input name="row$orgCons" value="f10h">
          <div class="objet-line">Logiciel</div>
          <time datetime="date-invalide"></time>
          <div class="date"><span class="day">x</span><span class="month">inconnu</span><span class="year">y</span></div>
        </div>
        """,
        """
        <div class="item_consultation">
          <input name="row$refCons" value="3032563">
          <input name="row$orgCons" value="f11h">
          <div class="objet-line">Logiciel</div>
          <time datetime="x"></time>
        </div>
        """,
    };

    public static TheoryData<string> PageSizeContractsThatMustNotPost
    {
        get
        {
            var page = SearchPage("3032564", "f12h", "10", "Fév.", "2026", 1, 1, 21);
            return new TheoryData<string>
            {
                page.Replace(
                    "<option selected=\"selected\" value=\"10\">",
                    "<option value=\"10\">",
                    StringComparison.Ordinal).Replace(
                    "<option value=\"20\">",
                    "<option selected=\"selected\" value=\"20\">",
                    StringComparison.Ordinal),
                page.Replace("<option value=\"20\">20</option>", string.Empty, StringComparison.Ordinal),
                page.Replace("name=\"PRADO_PAGESTATE\"", "name=\"OTHER_STATE\"", StringComparison.Ordinal),
            };
        }
    }

    private static PlaceSource CreateSource(HttpMessageHandler handler) => new(
        new HttpClient(handler) { BaseAddress = new Uri("https://place.example/") },
        Options.Create(new PlaceOptions
        {
            BaseUrl = "https://place.example/",
            RequestDelayMilliseconds = 0,
        }));

    private static SourcingSettings CreateSettings(string keyword) => new(
        TestSourcingProfiles.Create(keywords: [keyword]) with
        {
            CpvWhitelistPrefixes = [],
            CpvWatchPrefixes = [],
        },
        DateTimeOffset.UtcNow);

    private static string SearchPage(
        string id,
        string organization,
        string day,
        string month,
        string year,
        int currentPage,
        int pageCount,
        int advertisedCount = 2) => $$"""
        <html><body>
          <form action="/entreprise/login"><input name="identifier"></form>
          <form action="/?page=Entreprise.EntrepriseAdvancedSearch&amp;searchAnnCons&amp;keyWord=logiciel">
            <input name="PRADO_PAGESTATE" value="state-{{currentPage}}">
            <select id="ctl0_CONTENU_PAGE_resultSearch_listePageSizeTop">
              <option selected="selected" value="10">10</option>
              <option value="20">20</option>
            </select>
            <input id="ctl0_CONTENU_PAGE_resultSearch_numPageTop" value="{{currentPage}}">
            <span id="ctl0_CONTENU_PAGE_resultSearch_nombrePageTop">{{pageCount}}</span>
            <span id="ctl0_CONTENU_PAGE_resultSearch_nombreElement">{{advertisedCount}}</span>
            {{(currentPage < pageCount ? "<a id=\"ctl0_CONTENU_PAGE_resultSearch_PagerTop_ctl2\">Suivante</a>" : string.Empty)}}
            {{(currentPage < pageCount ? "<script>new Prado.WebUI.TLinkButton({'ID':\"ctl0_CONTENU_PAGE_resultSearch_PagerTop_ctl2\",'EventTarget':\"ctl0$CONTENU_PAGE$resultSearch$PagerTop$ctl2\"});</script>" : string.Empty)}}
            <div class="item_consultation">
              <input name="row$refCons" value="{{id}}">
              <input name="row$orgCons" value="{{organization}}">
              <div class="date"><span class="day">{{day}}</span><span class="month">{{month}}</span><span class="year">{{year}}</span></div>
              <div class="objet-line"><span class="truncate"><span title="Développement d’un logiciel métier">Titre</span></span></div>
              <div class="panelBlocObjet"><div title="Maintenance d’une application">Objet</div></div>
              <div class="panelBlocDenomination"><div title="Direction des achats">Acheteur</div></div>
              <div class="cons_procedure"><abbr title="Procédure ouverte">AO</abbr></div>
              <div class="cons_categorie">Services</div>
              <div class="lieux-exe">Paris (75)</div>
            </div>
          </form>
        </body></html>
        """;

    private static string DetailPage(string cpv, string? boampReference) => $$"""
        <html><body>
          <div><label>Objet</label><span>Description complète du besoin</span></div>
          <div><label>Procédure</label><span>Appel d'offres ouvert</span></div>
          <div><label>Catégorie</label><span>Services</span></div>
          <div><label>Date limite de remise des plis</label><span class="green bold">20/08/2026 12:00</span></div>
          <span data-code-cpv="{{cpv}}">{{cpv}} (Code principal)</span>
          <a id="linkDownloadDce" href="/index.php?page=Entreprise.EntrepriseDemandeTelechargementDce&amp;id=3032554">DCE</a>
          {{(boampReference is null ? string.Empty : $"<a href=\"https://www.boamp.fr/avis/{boampReference}\">BOAMP {boampReference}</a>")}}
        </body></html>
        """;

    private sealed class PlaceSequenceHandler(params string[] responses) : HttpMessageHandler
    {
        private readonly Queue<string> _responses = new(responses);

        public List<RequestRecord> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(new RequestRecord(request.Method, request.RequestUri!, body));
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_responses.Dequeue(), Encoding.UTF8, "text/html"),
                RequestMessage = request,
            };
        }
    }

    private sealed record RequestRecord(HttpMethod Method, Uri Uri, string Body);
}
