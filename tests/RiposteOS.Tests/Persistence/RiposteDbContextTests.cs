using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Pgvector.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using RiposteOS.Core.Consultations;
using RiposteOS.Core.Sourcing;
using RiposteOS.Core.Documents;
using RiposteOS.Infrastructure.Persistence;
using RiposteOS.Infrastructure.Persistence.Configurations;

namespace RiposteOS.Tests.Persistence;

public sealed class RiposteDbContextTests
{
    [Fact]
    public void ModelDiscoversEveryConfiguredApplicationEntity()
    {
        using var dbContext = new RiposteDbContext(
            new DbContextOptionsBuilder<RiposteDbContext>()
                .UseNpgsql(
                    "Host=localhost;Database=model_test;Username=test;Password=test",
                    options => options.UseVector())
                .Options);

        var opportunity = AssertEntity<Opportunity>(dbContext.Model, "opportunities", DatabaseSchemas.Sourcing);
        var revision = AssertEntity<OpportunityRevision>(
            dbContext.Model,
            "opportunity_revisions",
            DatabaseSchemas.Sourcing);
        var publication = AssertEntity<OpportunityPublication>(
            dbContext.Model,
            "opportunity_publications",
            DatabaseSchemas.Sourcing);
        var settings = AssertEntity<SourcingSettings>(dbContext.Model, "sourcing_settings", DatabaseSchemas.Sourcing);
        var run = AssertEntity<ImportRun>(dbContext.Model, "import_runs", DatabaseSchemas.Sourcing);
        var syncState = AssertEntity<SourcingSyncState>(dbContext.Model, "sourcing_sync_states", DatabaseSchemas.Sourcing);
        var document = AssertEntity<StoredDocument>(dbContext.Model, "stored_documents", DatabaseSchemas.Documents);
        var consultation = AssertEntity<Consultation>(
            dbContext.Model,
            "consultations",
            DatabaseSchemas.Consultations);
        var consultationDocument = AssertEntity<ConsultationDocument>(
            dbContext.Model,
            "consultation_documents",
            DatabaseSchemas.Consultations);

        Assert.Equal("jsonb", opportunity.FindProperty(nameof(Opportunity.RawPayload))?.GetColumnType());
        Assert.Equal("text[]", opportunity.FindProperty("_countryCodes")?.GetColumnType());
        Assert.Equal("jsonb", revision.FindProperty(nameof(OpportunityRevision.RawPayload))?.GetColumnType());
        Assert.Equal("jsonb", publication.FindProperty(nameof(OpportunityPublication.RawPayload))?.GetColumnType());
        Assert.Contains(publication.GetIndexes(), index => index.IsUnique);
        Assert.Equal(
            nameof(OpportunityRevision.OpportunityId),
            Assert.Single(revision.GetForeignKeys()).Properties.Single().Name);
        Assert.Equal(DatabaseFunctions.NewGuid, opportunity.FindProperty(nameof(Opportunity.Id))?.GetDefaultValueSql());
        Assert.Equal(DatabaseFunctions.Now, opportunity.FindProperty(nameof(Opportunity.ImportedAt))?.GetDefaultValueSql());
        Assert.Contains(opportunity.GetIndexes(), index => index.IsUnique);
        Assert.Equal(ValueGenerated.Never, settings.FindProperty(nameof(SourcingSettings.Id))?.ValueGenerated);
        Assert.Equal("text[]", settings.FindProperty("_allowedCountryCodes")?.GetColumnType());
        Assert.Equal(
            SourcingSettings.DefaultSynchronizationCron,
            settings.FindProperty(nameof(SourcingSettings.BoampCron))?.GetDefaultValue());
        Assert.Equal(
            SourcingSettings.DefaultSynchronizationCron,
            settings.FindProperty(nameof(SourcingSettings.TedCron))?.GetDefaultValue());
        Assert.Equal(
            SourcingSettings.DefaultPlaceSynchronizationCron,
            settings.FindProperty(nameof(SourcingSettings.PlaceCron))?.GetDefaultValue());
        Assert.Equal(typeof(string), run.FindProperty(nameof(ImportRun.Status))?.GetProviderClrType());
        Assert.Equal(DatabaseFunctions.NewGuid, run.FindProperty(nameof(ImportRun.Id))?.GetDefaultValueSql());
        Assert.Equal(ValueGenerated.Never, syncState.FindProperty(nameof(SourcingSyncState.Source))?.ValueGenerated);
        Assert.Equal("bigint", document.FindProperty(nameof(StoredDocument.Size))?.GetColumnType());
        Assert.Contains(document.GetIndexes(), index => index.IsUnique);
        Assert.Equal(DatabaseFunctions.NewGuid, consultation.FindProperty(nameof(Consultation.Id))?.GetDefaultValueSql());
        Assert.Equal(DatabaseFunctions.Now, consultation.FindProperty(nameof(Consultation.CreatedAt))?.GetDefaultValueSql());
        Assert.Contains(
            consultation.GetIndexes(),
            index => index.IsUnique
                && index.GetFilter() == "\"OpportunityId\" IS NOT NULL");
        Assert.Equal(
            DeleteBehavior.Restrict,
            Assert.Single(consultation.GetForeignKeys()).DeleteBehavior);
        Assert.Equal(typeof(string), consultationDocument.FindProperty(nameof(ConsultationDocument.Kind))?.GetProviderClrType());
        Assert.Equal(DatabaseFunctions.Now, consultationDocument.FindProperty(nameof(ConsultationDocument.AddedAt))?.GetDefaultValueSql());
        Assert.Equal(2, consultationDocument.FindPrimaryKey()?.Properties.Count);
        Assert.All(
            consultationDocument.GetForeignKeys(),
            foreignKey => Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior));

        var identityUser = dbContext.Model.FindEntityType(typeof(IdentityUser<Guid>));
        var identityRole = dbContext.Model.FindEntityType(typeof(IdentityRole<Guid>));
        Assert.Equal(DatabaseSchemas.Identity, identityUser?.GetSchema());
        Assert.Equal(DatabaseFunctions.NewGuid, identityUser?.FindProperty(nameof(IdentityUser<Guid>.Id))?.GetDefaultValueSql());
        Assert.Equal(DatabaseSchemas.Identity, identityRole?.GetSchema());
        Assert.Equal(DatabaseFunctions.NewGuid, identityRole?.FindProperty(nameof(IdentityRole<Guid>.Id))?.GetDefaultValueSql());
    }

    [Fact]
    public void DesignTimeFactoryCreatesPostgreSqlContext()
    {
        using var dbContext = new RiposteDbContextFactory().CreateDbContext([]);

        Assert.Equal("Npgsql.EntityFrameworkCore.PostgreSQL", dbContext.Database.ProviderName);
    }

    [Fact]
    public void DesignTimeFactoryHonorsTheConfiguredConnectionString()
    {
        const string Variable = "ConnectionStrings__Database";
        var previous = Environment.GetEnvironmentVariable(Variable);
        try
        {
            Environment.SetEnvironmentVariable(
                Variable,
                "Host=configured-host;Database=riposteos;Username=test;Password=test");

            using var dbContext = new RiposteDbContextFactory().CreateDbContext([]);

            Assert.Contains("configured-host", dbContext.Database.GetConnectionString(), StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable(Variable, previous);
        }
    }

    private static IEntityType AssertEntity<TEntity>(IModel model, string tableName, string schema)
    {
        var entity = model.FindEntityType(typeof(TEntity));
        Assert.NotNull(entity);
        Assert.Equal(tableName, entity.GetTableName());
        Assert.Equal(schema, entity.GetSchema());
        return entity;
    }
}
