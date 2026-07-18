using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Net;
using System.Net.Http.Json;
using RiposteOS.Api.Consultations.Dtos;
using RiposteOS.Api.Sourcing.Dtos;
using RiposteOS.Core.Consultations;
using RiposteOS.Core.Documents;
using RiposteOS.Core.Sourcing;
using RiposteOS.Infrastructure.Consultations;
using RiposteOS.Infrastructure.Persistence;
using RiposteOS.Tests.TestSupport;

namespace RiposteOS.Tests.Consultations;

[Collection(PostgreSqlTestGroup.Name)]
public sealed class PostgreSqlConsultationsTests(PostgreSqlFixture fixture)
{
    private static readonly DateTimeOffset Now = new(2026, 7, 17, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task MigrationCreatesConsultationSchemaTablesAndPartialUniqueIndex()
    {
        var connectionString = await fixture.CreateDatabaseAsync();
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            SELECT
                to_regclass('consultations.consultations')::text,
                to_regclass('consultations.consultation_documents')::text,
                indexdef
            FROM pg_indexes
            WHERE schemaname = 'consultations'
              AND indexname = 'ux_consultations_opportunity_id';
            """,
            connection);
        await using var reader = await command.ExecuteReaderAsync();

        Assert.True(await reader.ReadAsync());
        Assert.Equal("consultations.consultations", reader.GetString(0));
        Assert.Equal("consultations.consultation_documents", reader.GetString(1));
        var indexDefinition = reader.GetString(2);
        Assert.Contains("UNIQUE", indexDefinition, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("WHERE (\"OpportunityId\" IS NOT NULL)", indexDefinition, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OpportunityAndDocumentLinksAreProtectedByPostgreSqlForeignKeys()
    {
        var connectionString = await fixture.CreateDatabaseAsync();
        await using var dbContext = PostgreSqlFixture.CreateContext(connectionString);
        var orphan = new Consultation("Orpheline", "Acheteur", null, null, Now);
        orphan.ReassignToOpportunity(Guid.NewGuid(), Now);
        dbContext.Add(orphan);

        var opportunityException = await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
        Assert.Equal(
            PostgresErrorCodes.ForeignKeyViolation,
            Assert.IsType<PostgresException>(opportunityException.InnerException).SqlState);

        dbContext.ChangeTracker.Clear();
        var consultation = new Consultation("Valide", "Acheteur", null, null, Now);
        dbContext.Add(consultation);
        await dbContext.SaveChangesAsync();
        dbContext.Add(new ConsultationDocument(
            consultation.Id,
            Guid.NewGuid(),
            ConsultationDocumentKind.Other,
            Now));

        var documentException = await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
        Assert.Equal(
            PostgresErrorCodes.ForeignKeyViolation,
            Assert.IsType<PostgresException>(documentException.InnerException).SqlState);
    }

    [Fact]
    public async Task PostgreSqlPreventsDuplicateOpportunityAndDocumentLinks()
    {
        var connectionString = await fixture.CreateDatabaseAsync();
        await using var dbContext = PostgreSqlFixture.CreateContext(connectionString);
        var opportunity = CreateOpportunity("unique");
        var storedDocument = CreateStoredDocument();
        dbContext.AddRange(opportunity, storedDocument);
        await dbContext.SaveChangesAsync();
        var first = Consultation.FromOpportunity(opportunity, Now);
        dbContext.Add(first);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        dbContext.Add(Consultation.FromOpportunity(opportunity, Now));

        var consultationException = await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
        Assert.Equal(
            PostgresErrorCodes.UniqueViolation,
            Assert.IsType<PostgresException>(consultationException.InnerException).SqlState);

        dbContext.ChangeTracker.Clear();
        dbContext.Add(new ConsultationDocument(first.Id, storedDocument.Id, ConsultationDocumentKind.FullDce, Now));
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        dbContext.Add(new ConsultationDocument(first.Id, storedDocument.Id, ConsultationDocumentKind.Other, Now));

        var documentException = await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
        Assert.Equal(
            PostgresErrorCodes.UniqueViolation,
            Assert.IsType<PostgresException>(documentException.InnerException).SqlState);
    }

    [Fact]
    public async Task PostgreSqlFacadeAttachesUpdatesAndDetachesDocuments()
    {
        var connectionString = await fixture.CreateDatabaseAsync();
        await using var dbContext = PostgreSqlFixture.CreateContext(connectionString);
        var consultation = new Consultation("Logiciel métier", "Acheteur", null, null, Now);
        var storedDocument = CreateStoredDocument();
        dbContext.AddRange(consultation, storedDocument);
        await dbContext.SaveChangesAsync();
        var facade = new ConsultationsFacade(dbContext, new FixedTimeProvider(Now));

        var first = await facade.AttachDocumentAsync(
            consultation.Id,
            storedDocument.Id,
            ConsultationDocumentKind.FullDce,
            CancellationToken.None);
        var second = await facade.AttachDocumentAsync(
            consultation.Id,
            storedDocument.Id,
            ConsultationDocumentKind.FullDce,
            CancellationToken.None);
        var documents = await facade.ListDocumentsAsync(
            consultation.Id,
            CancellationToken.None);
        var updated = await facade.ChangeDocumentKindAsync(
            consultation.Id,
            storedDocument.Id,
            ConsultationDocumentKind.TechnicalSpecifications,
            CancellationToken.None);
        var detached = await facade.DetachDocumentAsync(
            consultation.Id,
            storedDocument.Id,
            CancellationToken.None);
        var missingDetach = await facade.DetachDocumentAsync(
            consultation.Id,
            storedDocument.Id,
            CancellationToken.None);
        var emptyDocuments = await facade.ListDocumentsAsync(
            consultation.Id,
            CancellationToken.None);

        Assert.Equal(ConsultationDocumentAttachmentStatus.Created, first.Status);
        Assert.Equal(ConsultationDocumentAttachmentStatus.Existing, second.Status);
        Assert.Equal(storedDocument.Id, Assert.Single(documents!).Id);
        Assert.Equal(ConsultationDocumentKind.TechnicalSpecifications, updated!.Kind);
        Assert.True(detached);
        Assert.False(missingDetach);
        Assert.Empty(emptyDocuments!);
    }

    [Fact]
    public async Task PostgreSqlFacadeListsFiltersAndGetsConsultations()
    {
        var connectionString = await fixture.CreateDatabaseAsync();
        await using var dbContext = PostgreSqlFixture.CreateContext(connectionString);
        var opportunity = CreateOpportunity("list");
        dbContext.Add(opportunity);
        await dbContext.SaveChangesAsync();
        var sourced = Consultation.FromOpportunity(opportunity, Now);
        var manual = new Consultation("Portail citoyen", "Ville de Lyon", null, null, Now);
        dbContext.AddRange(sourced, manual);
        await dbContext.SaveChangesAsync();
        var facade = new ConsultationsFacade(dbContext, new FixedTimeProvider(Now));

        var page = await facade.ListAsync(1, 1, null, "title", CancellationToken.None);
        var filtered = await facade.ListAsync(1, 20, "source=BOAMP", "title", CancellationToken.None);
        var detail = await facade.GetAsync(sourced.Id, CancellationToken.None);

        Assert.Equal(2, page.TotalCount);
        Assert.Equal("Logiciel métier", Assert.Single(page.Items).Title);
        Assert.Equal(sourced.Id, Assert.Single(filtered.Items).Id);
        Assert.Equal("BOAMP", detail!.Source);
        Assert.Equal(opportunity.SourceId, detail.SourceId);
    }

    [Fact]
    public async Task ConcurrentPromotionsReturnOneCreatedConsultationAndOneExistingConsultation()
    {
        var connectionString = await fixture.CreateDatabaseAsync();
        Guid opportunityId;
        await using (var seedContext = PostgreSqlFixture.CreateContext(connectionString))
        {
            var opportunity = CreateOpportunity("concurrent");
            seedContext.Add(opportunity);
            await seedContext.SaveChangesAsync();
            opportunityId = opportunity.Id;
        }

        await using var firstContext = PostgreSqlFixture.CreateContext(connectionString);
        await using var secondContext = PostgreSqlFixture.CreateContext(connectionString);
        var firstFacade = new ConsultationsFacade(firstContext, new FixedTimeProvider(Now));
        var secondFacade = new ConsultationsFacade(secondContext, new FixedTimeProvider(Now));

        var results = await Task.WhenAll(
            firstFacade.PromoteOpportunityAsync(opportunityId, CancellationToken.None),
            secondFacade.PromoteOpportunityAsync(opportunityId, CancellationToken.None));

        Assert.Single(results, result => result.Created);
        Assert.Single(results, result => !result.Created);
        Assert.All(results, result => Assert.NotNull(result.Consultation));
        Assert.Equal(results[0].Consultation!.Id, results[1].Consultation!.Id);
        await using var verificationContext = PostgreSqlFixture.CreateContext(connectionString);
        Assert.Single(await verificationContext.Set<Consultation>().ToArrayAsync());
        Assert.Equal(
            OpportunityStatus.Retained,
            (await verificationContext.Set<Opportunity>().SingleAsync()).Status);
    }

    [Fact]
    public async Task ConcurrentHttpPromotionsAreIdempotentAgainstPostgreSql()
    {
        var connectionString = await fixture.CreateDatabaseAsync();
        Guid opportunityId;
        await using (var seedContext = PostgreSqlFixture.CreateContext(connectionString))
        {
            var opportunity = CreateOpportunity("concurrent-http");
            seedContext.Add(opportunity);
            await seedContext.SaveChangesAsync();
            opportunityId = opportunity.Id;
        }

        await using var factory = new PostgreSqlWebApplicationFactory(connectionString);
        var firstClient = factory.CreateClient();
        var secondClient = factory.CreateClient();

        var responses = await Task.WhenAll(
            firstClient.PostAsync($"/api/opportunities/{opportunityId}/consultation", null),
            secondClient.PostAsync($"/api/opportunities/{opportunityId}/consultation", null));
        var consultations = await Task.WhenAll(
            responses[0].Content.ReadFromJsonAsync<ConsultationResponse>(),
            responses[1].Content.ReadFromJsonAsync<ConsultationResponse>());

        Assert.Contains(responses, response => response.StatusCode == HttpStatusCode.Created);
        Assert.Contains(responses, response => response.StatusCode == HttpStatusCode.OK);
        Assert.Equal(consultations[0]!.Id, consultations[1]!.Id);
        await using var verificationContext = PostgreSqlFixture.CreateContext(connectionString);
        Assert.Single(await verificationContext.Set<Consultation>().ToArrayAsync());
    }

    [Fact]
    public async Task PromotionRacingWithDismissalCannotLeaveADismissedOpenStudy()
    {
        var connectionString = await fixture.CreateDatabaseAsync();
        Guid opportunityId;
        await using (var seedContext = PostgreSqlFixture.CreateContext(connectionString))
        {
            var opportunity = CreateOpportunity("promotion-status-race");
            seedContext.Add(opportunity);
            await seedContext.SaveChangesAsync();
            opportunityId = opportunity.Id;
        }

        await using var factory = new PostgreSqlWebApplicationFactory(connectionString);
        var promotion = factory.CreateClient().PostAsync(
            $"/api/opportunities/{opportunityId}/consultation",
            null);
        var dismissal = factory.CreateClient().PutAsJsonAsync(
            $"/api/opportunities/{opportunityId}/status",
            new OpportunityStatusRequest("Dismissed"));

        var responses = await Task.WhenAll(promotion, dismissal);

        Assert.Equal(HttpStatusCode.Created, responses[0].StatusCode);
        Assert.Contains(
            responses[1].StatusCode,
            new[] { HttpStatusCode.OK, HttpStatusCode.Conflict });
        await using var verificationContext = PostgreSqlFixture.CreateContext(connectionString);
        Assert.Single(await verificationContext.Set<Consultation>().ToArrayAsync());
        Assert.Equal(
            OpportunityStatus.Retained,
            (await verificationContext.Set<Opportunity>().SingleAsync()).Status);
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

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
