using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RiposteOS.Api.Consultations.Dtos;
using RiposteOS.Api.Sourcing.Dtos;
using RiposteOS.Core.Consultations;
using RiposteOS.Core.Documents;
using RiposteOS.Core.Sourcing;
using RiposteOS.Infrastructure.Persistence;
using RiposteOS.Tests.TestSupport;

namespace RiposteOS.Tests.Api;

public sealed class ConsultationsEndpointsTests(RiposteWebApplicationFactory factory)
    : IClassFixture<RiposteWebApplicationFactory>
{
    private static readonly DateTimeOffset Now = new(2026, 7, 17, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ManualConsultationIsCreatedWithoutASourcingOpportunity()
    {
        await factory.ResetAsync();
        var deadline = Now.AddDays(10);

        using var response = await factory.CreateClient().PostAsJsonAsync(
            "/api/consultations",
            new CreateConsultationRequest(
                "  Portail citoyen  ",
                "  Ville de Lyon  ",
                deadline,
                " https://example.test/avis "));
        var consultation = await response.Content.ReadFromJsonAsync<ConsultationResponse>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal($"/api/consultations/{consultation!.Id}", response.Headers.Location?.OriginalString);
        Assert.Null(consultation.OpportunityId);
        Assert.Null(consultation.Source);
        Assert.Null(consultation.SourceId);
        Assert.Equal("Portail citoyen", consultation.Title);
        Assert.Equal("Ville de Lyon", consultation.Buyer);
        Assert.Equal(deadline, consultation.ResponseDeadline);
        Assert.Equal("https://example.test/avis", consultation.NoticeUrl);
    }

    [Theory]
    [InlineData("", "Acheteur", null)]
    [InlineData("Titre", " ", null)]
    [InlineData("Titre", "Acheteur", "/avis/26-1")]
    [InlineData("Titre", "Acheteur", "file:///tmp/avis")]
    public async Task ManualConsultationValidatesRequiredFieldsAndNoticeUrl(
        string title,
        string buyer,
        string? noticeUrl)
    {
        await factory.ResetAsync();

        using var response = await factory.CreateClient().PostAsJsonAsync(
            "/api/consultations",
            new CreateConsultationRequest(title, buyer, null, noticeUrl));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ManualConsultationRejectsOverlongFields()
    {
        await factory.ResetAsync();
        var client = factory.CreateClient();

        using var title = await client.PostAsJsonAsync(
            "/api/consultations",
            new CreateConsultationRequest(
                new string('a', Consultation.MaximumTitleLength + 1),
                "Acheteur",
                null,
                null));
        using var buyer = await client.PostAsJsonAsync(
            "/api/consultations",
            new CreateConsultationRequest(
                "Titre",
                new string('a', Consultation.MaximumBuyerLength + 1),
                null,
                null));
        using var noticeUrl = await client.PostAsJsonAsync(
            "/api/consultations",
            new CreateConsultationRequest(
                "Titre",
                "Acheteur",
                null,
                $"https://example.test/{new string('a', Consultation.MaximumNoticeUrlLength)}"));

        Assert.Equal(HttpStatusCode.BadRequest, title.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, buyer.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, noticeUrl.StatusCode);
    }

    [Fact]
    public async Task OpportunityPromotionIsIdempotentAndLocksQualificationStatus()
    {
        await factory.ResetAsync();
        var opportunity = CreateOpportunity("promotion");
        await SeedAsync(opportunity);
        var client = factory.CreateClient();

        using var firstResponse = await client.PostAsync(
            $"/api/opportunities/{opportunity.Id}/consultation",
            null);
        var first = await firstResponse.Content.ReadFromJsonAsync<ConsultationResponse>();
        using var secondResponse = await client.PostAsync(
            $"/api/opportunities/{opportunity.Id}/consultation",
            null);
        var second = await secondResponse.Content.ReadFromJsonAsync<ConsultationResponse>();
        using var retainedDirectly = await client.PutAsJsonAsync(
            $"/api/opportunities/{opportunity.Id}/status",
            new OpportunityStatusRequest("Retained"));
        using var dismissed = await client.PutAsJsonAsync(
            $"/api/opportunities/{opportunity.Id}/status",
            new OpportunityStatusRequest("Dismissed"));
        using var returned = await client.PutAsJsonAsync(
            $"/api/opportunities/{opportunity.Id}/status",
            new OpportunityStatusRequest("ToQualify"));

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal($"/api/consultations/{first!.Id}", firstResponse.Headers.Location?.OriginalString);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        Assert.Equal(first.Id, second!.Id);
        Assert.Equal(opportunity.Id, first.OpportunityId);
        Assert.Equal(opportunity.Source, first.Source);
        Assert.Equal(opportunity.SourceId, first.SourceId);
        Assert.Equal(HttpStatusCode.BadRequest, retainedDirectly.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, dismissed.StatusCode);
        Assert.Contains("étude est ouverte", await dismissed.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Conflict, returned.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RiposteDbContext>();
        Assert.Equal(
            OpportunityStatus.Retained,
            (await dbContext.Set<Opportunity>().SingleAsync()).Status);
        Assert.Single(await dbContext.Set<Consultation>().ToArrayAsync());
    }

    [Fact]
    public async Task MissingOpportunityCannotBePromoted()
    {
        await factory.ResetAsync();

        using var response = await factory.CreateClient().PostAsync(
            $"/api/opportunities/{Guid.NewGuid()}/consultation",
            null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ConsultationsAreFilteredOrderedAndPaginatedInTheApiContract()
    {
        await factory.ResetAsync();
        var client = factory.CreateClient();
        await client.PostAsJsonAsync(
            "/api/consultations",
            new CreateConsultationRequest("Portail citoyen", "Ville de Lyon", Now.AddDays(20), null));
        await client.PostAsJsonAsync(
            "/api/consultations",
            new CreateConsultationRequest("Logiciel métier", "Métropole de Lyon", Now.AddDays(10), null));

        var firstPage = await client.GetFromJsonAsync<ConsultationListResponse>(
            "/api/consultations?page=1&pageSize=1&orderBy=title");
        var secondPage = await client.GetFromJsonAsync<ConsultationListResponse>(
            "/api/consultations?page=2&pageSize=1&orderBy=title");
        var filtered = await client.GetFromJsonAsync<ConsultationListResponse>(
            "/api/consultations?filter=buyer=*m%C3%A9tropole/i");

        Assert.Equal(2, firstPage!.TotalCount);
        Assert.Equal("Logiciel métier", Assert.Single(firstPage.Items).Title);
        Assert.Equal("Portail citoyen", Assert.Single(secondPage!.Items).Title);
        Assert.Equal("Logiciel métier", Assert.Single(filtered!.Items).Title);
        Assert.Equal(1, filtered.TotalCount);
    }

    [Theory]
    [InlineData("/api/consultations?page=0")]
    [InlineData("/api/consultations?pageSize=0")]
    [InlineData("/api/consultations?pageSize=101")]
    [InlineData("/api/consultations?filter=opportunityId=null")]
    [InlineData("/api/consultations?orderBy=opportunityId")]
    public async Task ConsultationsRejectInvalidPaginationAndInternalQueries(string path)
    {
        await factory.ResetAsync();

        using var response = await factory.CreateClient().GetAsync(path);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ConsultationsRejectOversizedQueries()
    {
        await factory.ResetAsync();
        var client = factory.CreateClient();

        using var filter = await client.GetAsync(
            $"/api/consultations?filter={new string('a', 2_001)}");
        using var orderBy = await client.GetAsync(
            $"/api/consultations?orderBy={new string('a', 201)}");

        Assert.Equal(HttpStatusCode.BadRequest, filter.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, orderBy.StatusCode);
    }

    [Fact]
    public async Task ConsultationDetailReturnsDocumentCountAndMissingConsultationReturns404()
    {
        await factory.ResetAsync();
        var consultation = new Consultation("Logiciel métier", "Acheteur", null, null, Now);
        var storedDocument = CreateStoredDocument();
        await SeedAsync(consultation, storedDocument);
        await SeedAsync(new ConsultationDocument(
            consultation.Id,
            storedDocument.Id,
            ConsultationDocumentKind.FullDce,
            Now));
        var client = factory.CreateClient();

        var detail = await client.GetFromJsonAsync<ConsultationResponse>(
            $"/api/consultations/{consultation.Id}");
        using var missing = await client.GetAsync($"/api/consultations/{Guid.NewGuid()}");

        Assert.Equal(1, detail!.DocumentCount);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task DocumentCanBeAttachedListedAndAttachedIdempotently()
    {
        await factory.ResetAsync();
        var consultation = new Consultation("Logiciel métier", "Acheteur", null, null, Now);
        var storedDocument = CreateStoredDocument();
        await SeedAsync(consultation, storedDocument);
        var request = new AttachConsultationDocumentRequest(
            storedDocument.Id,
            ConsultationDocumentKind.TechnicalSpecifications.ToString());
        var client = factory.CreateClient();

        using var firstResponse = await client.PostAsJsonAsync(
            $"/api/consultations/{consultation.Id}/documents",
            request);
        var first = await firstResponse.Content.ReadFromJsonAsync<ConsultationDocumentResponse>();
        using var secondResponse = await client.PostAsJsonAsync(
            $"/api/consultations/{consultation.Id}/documents",
            request);
        var documents = await client.GetFromJsonAsync<ConsultationDocumentResponse[]>(
            $"/api/consultations/{consultation.Id}/documents");

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        Assert.Equal(storedDocument.Id, first!.Id);
        Assert.Equal("TechnicalSpecifications", first.Kind);
        Assert.Equal("Manual", first.KindOrigin);
        Assert.Equal($"/api/documents/{storedDocument.Id}/content", first.DownloadUrl);
        Assert.Single(documents!);
    }

    [Fact]
    public async Task DevelopmentEndpointReturnsStoredDocumentPassagesInOrdinalOrder()
    {
        await factory.ResetAsync();
        var consultation = new Consultation("Logiciel métier", "Acheteur", null, null, Now);
        var storedDocument = CreateStoredDocument();
        var run = new DocumentProcessingRun(storedDocument.Id, Now);
        await SeedAsync(consultation, storedDocument, run);
        await SeedAsync(new ConsultationDocument(
            consultation.Id,
            storedDocument.Id,
            ConsultationDocumentKind.FullDce,
            Now));
        run.TryStart(Now.AddMinutes(1));
        run.Complete(2, 2, Now.AddMinutes(2));
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<RiposteDbContext>();
            dbContext.Update(run);
            dbContext.AddRange(
                new DocumentPassage(run.Id, 2, "Second passage", 2, null, null),
                new DocumentPassage(run.Id, 1, "Premier passage", 1, "Introduction", null));
            await dbContext.SaveChangesAsync();
        }

        var passages = await factory.CreateClient().GetFromJsonAsync<DocumentAnalysisPassageResponse[]>(
            $"/api/consultations/{consultation.Id}/documents/{storedDocument.Id}/analysis/passages");

        Assert.Collection(
            passages!,
            passage =>
            {
                Assert.Equal(1, passage.Ordinal);
                Assert.Equal("Premier passage", passage.Text);
                Assert.Equal(1, passage.PageNumber);
                Assert.Equal("Introduction", passage.SectionTitle);
            },
            passage => Assert.Equal(2, passage.Ordinal));
    }

    [Fact]
    public async Task DocumentAnalysisCanBeQueuedAndIsIdempotent()
    {
        await factory.ResetAsync();
        var consultation = new Consultation("Logiciel métier", "Acheteur", null, null, Now);
        var storedDocument = CreateStoredDocument();
        await SeedAsync(consultation, storedDocument);
        await SeedAsync(new ConsultationDocument(
            consultation.Id,
            storedDocument.Id,
            ConsultationDocumentKind.FullDce,
            Now));
        var client = factory.CreateClient();

        using var queued = await client.PostAsync(
            $"/api/consultations/{consultation.Id}/documents/{storedDocument.Id}/analysis",
            null);
        using var existing = await client.PostAsync(
            $"/api/consultations/{consultation.Id}/documents/{storedDocument.Id}/analysis",
            null);

        Assert.Equal(HttpStatusCode.Accepted, queued.StatusCode);
        Assert.Equal(HttpStatusCode.OK, existing.StatusCode);
        Assert.NotNull(factory.Jobs.CreatedJob);
    }

    [Fact]
    public async Task DocumentAnalysisRejectsMissingAndUnsupportedDocuments()
    {
        await factory.ResetAsync();
        var consultation = new Consultation("Logiciel métier", "Acheteur", null, null, Now);
        var unsupported = new StoredDocument(
            Guid.NewGuid(),
            "archive.zip",
            "application/zip",
            12,
            new string('a', 64),
            Now);
        await SeedAsync(consultation, unsupported);
        await SeedAsync(new ConsultationDocument(
            consultation.Id,
            unsupported.Id,
            ConsultationDocumentKind.Other,
            Now));
        var client = factory.CreateClient();

        using var missing = await client.PostAsync(
            $"/api/consultations/{consultation.Id}/documents/{Guid.NewGuid()}/analysis",
            null);
        using var unsupportedResponse = await client.PostAsync(
            $"/api/consultations/{consultation.Id}/documents/{unsupported.Id}/analysis",
            null);

        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, unsupportedResponse.StatusCode);
    }

    [Fact]
    public async Task DocumentAnalysisRecordsAWorkerQueueFailure()
    {
        await factory.ResetAsync();
        var consultation = new Consultation("Logiciel métier", "Acheteur", null, null, Now);
        var storedDocument = CreateStoredDocument();
        await SeedAsync(consultation, storedDocument);
        await SeedAsync(new ConsultationDocument(
            consultation.Id,
            storedDocument.Id,
            ConsultationDocumentKind.FullDce,
            Now));
        factory.Jobs.ThrowOnCreate = true;

        using var response = await factory.CreateClient().PostAsync(
            $"/api/consultations/{consultation.Id}/documents/{storedDocument.Id}/analysis",
            null);
        var document = await response.Content.ReadFromJsonAsync<ConsultationDocumentResponse>();

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal("Failed", document!.Analysis.Status);
        Assert.Equal("L'analyse n'a pas pu être transmise au worker.", document.Analysis.ErrorMessage);
    }

    [Fact]
    public async Task DocumentCategoryCanBeChangedAndDocumentCanBeDetached()
    {
        await factory.ResetAsync();
        var consultation = new Consultation("Logiciel métier", "Acheteur", null, null, Now);
        var storedDocument = CreateStoredDocument();
        await SeedAsync(consultation, storedDocument);
        var client = factory.CreateClient();
        await client.PostAsJsonAsync(
            $"/api/consultations/{consultation.Id}/documents",
            new AttachConsultationDocumentRequest(storedDocument.Id, "Other"));

        using var updatedResponse = await client.PutAsJsonAsync(
            $"/api/consultations/{consultation.Id}/documents/{storedDocument.Id}",
            new UpdateConsultationDocumentRequest("Pricing"));
        var updated = await updatedResponse.Content.ReadFromJsonAsync<ConsultationDocumentResponse>();
        var listed = await client.GetFromJsonAsync<ConsultationDocumentResponse[]>(
            $"/api/consultations/{consultation.Id}/documents");
        using var detached = await client.DeleteAsync(
            $"/api/consultations/{consultation.Id}/documents/{storedDocument.Id}");
        var empty = await client.GetFromJsonAsync<ConsultationDocumentResponse[]>(
            $"/api/consultations/{consultation.Id}/documents");
        using var missing = await client.DeleteAsync(
            $"/api/consultations/{consultation.Id}/documents/{storedDocument.Id}");

        Assert.Equal(HttpStatusCode.OK, updatedResponse.StatusCode);
        Assert.Equal("Pricing", updated!.Kind);
        Assert.Equal("Pricing", Assert.Single(listed!).Kind);
        Assert.Equal(HttpStatusCode.NoContent, detached.StatusCode);
        Assert.Empty(empty!);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task DocumentCanBeAttachedWithoutAKindForAutomaticClassification()
    {
        await factory.ResetAsync();
        var consultation = new Consultation("Logiciel métier", "Acheteur", null, null, Now);
        var storedDocument = CreateStoredDocument();
        await SeedAsync(consultation, storedDocument);

        using var response = await factory.CreateClient().PostAsJsonAsync(
            $"/api/consultations/{consultation.Id}/documents",
            new AttachConsultationDocumentRequest(storedDocument.Id, null));
        var document = await response.Content.ReadFromJsonAsync<ConsultationDocumentResponse>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("Other", document!.Kind);
        Assert.Equal("Automatic", document.KindOrigin);
    }

    [Fact]
    public async Task AutomaticDocumentClassificationCanBeQueuedWithoutOverridingManualDocuments()
    {
        await factory.ResetAsync();
        var consultation = new Consultation("Logiciel métier", "Acheteur", null, null, Now);
        var automatic = CreateStoredDocument();
        var manual = CreateStoredDocument();
        await SeedAsync(consultation, automatic, manual);
        await SeedAsync(
            new ConsultationDocument(consultation.Id, automatic.Id, ConsultationDocumentKind.Other, ConsultationDocumentKindOrigin.Automatic, Now),
            new ConsultationDocument(consultation.Id, manual.Id, ConsultationDocumentKind.Pricing, Now));
        var client = factory.CreateClient();

        using var queued = await client.PostAsync($"/api/consultations/{consultation.Id}/documents/{automatic.Id}/classification", null);
        var queuedDocument = await queued.Content.ReadFromJsonAsync<ConsultationDocumentResponse>();
        using var manualResult = await client.PostAsync($"/api/consultations/{consultation.Id}/documents/{manual.Id}/classification", null);
        var manualDocument = await manualResult.Content.ReadFromJsonAsync<ConsultationDocumentResponse>();
        using var missing = await client.PostAsync($"/api/consultations/{consultation.Id}/documents/{Guid.NewGuid()}/classification", null);

        Assert.Equal(HttpStatusCode.Accepted, queued.StatusCode);
        Assert.Equal("Queued", queuedDocument!.Classification.Status);
        Assert.Equal(HttpStatusCode.Accepted, manualResult.StatusCode);
        Assert.Equal("NotStarted", manualDocument!.Classification.Status);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.NotNull(factory.Jobs.CreatedJob);
    }

    [Fact]
    public async Task DocumentAttachmentValidatesReferencesAndKind()
    {
        await factory.ResetAsync();
        var consultation = new Consultation("Logiciel métier", "Acheteur", null, null, Now);
        var storedDocument = CreateStoredDocument();
        await SeedAsync(consultation, storedDocument);
        var client = factory.CreateClient();

        using var missingConsultation = await client.PostAsJsonAsync(
            $"/api/consultations/{Guid.NewGuid()}/documents",
            new AttachConsultationDocumentRequest(storedDocument.Id, "Other"));
        using var missingDocument = await client.PostAsJsonAsync(
            $"/api/consultations/{consultation.Id}/documents",
            new AttachConsultationDocumentRequest(Guid.NewGuid(), "Other"));
        using var emptyDocument = await client.PostAsJsonAsync(
            $"/api/consultations/{consultation.Id}/documents",
            new AttachConsultationDocumentRequest(Guid.Empty, "Other"));
        using var invalidKind = await client.PostAsJsonAsync(
            $"/api/consultations/{consultation.Id}/documents",
            new AttachConsultationDocumentRequest(storedDocument.Id, "Unknown"));
        using var invalidUpdatedKind = await client.PutAsJsonAsync(
            $"/api/consultations/{consultation.Id}/documents/{storedDocument.Id}",
            new UpdateConsultationDocumentRequest("Unknown"));
        using var missingUpdate = await client.PutAsJsonAsync(
            $"/api/consultations/{consultation.Id}/documents/{Guid.NewGuid()}",
            new UpdateConsultationDocumentRequest("Other"));
        using var missingList = await client.GetAsync(
            $"/api/consultations/{Guid.NewGuid()}/documents");

        Assert.Equal(HttpStatusCode.NotFound, missingConsultation.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missingDocument.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, emptyDocument.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalidKind.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalidUpdatedKind.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missingUpdate.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missingList.StatusCode);
    }

    private static Opportunity CreateOpportunity(string sourceId) => new(
        SourcingSource.Boamp,
        sourceId,
        "Logiciel métier",
        "Métropole de Lyon",
        new DateOnly(2026, 7, 17),
        Now.AddDays(10),
        ["FRA"],
        ["69"],
        ["72200000"],
        [],
        [],
        50,
        ["CPV ciblé"],
        "https://example.test/avis",
        "{}",
        Now);

    private static StoredDocument CreateStoredDocument() => new(
        Guid.NewGuid(),
        "dce.pdf",
        "application/pdf",
        12,
        new string('a', 64),
        Now);

    private async Task SeedAsync(params object[] entities)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RiposteDbContext>();
        dbContext.AddRange(entities);
        await dbContext.SaveChangesAsync();
    }
}
