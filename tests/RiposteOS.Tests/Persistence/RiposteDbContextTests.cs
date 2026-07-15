using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.AspNetCore.Identity;
using RiposteOS.Core.Sourcing;
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
                .UseNpgsql("Host=localhost;Database=model_test;Username=test;Password=test")
                .Options);

        var opportunity = AssertEntity<Opportunity>(dbContext.Model, "opportunities", DatabaseSchemas.Sourcing);
        var settings = AssertEntity<SourcingSettings>(dbContext.Model, "sourcing_settings", DatabaseSchemas.Sourcing);
        var run = AssertEntity<ImportRun>(dbContext.Model, "import_runs", DatabaseSchemas.Sourcing);
        var syncState = AssertEntity<SourcingSyncState>(dbContext.Model, "sourcing_sync_states", DatabaseSchemas.Sourcing);

        Assert.Equal("jsonb", opportunity.FindProperty(nameof(Opportunity.RawPayload))?.GetColumnType());
        Assert.Equal(DatabaseFunctions.NewGuid, opportunity.FindProperty(nameof(Opportunity.Id))?.GetDefaultValueSql());
        Assert.Equal(DatabaseFunctions.Now, opportunity.FindProperty(nameof(Opportunity.ImportedAt))?.GetDefaultValueSql());
        Assert.Contains(opportunity.GetIndexes(), index => index.IsUnique);
        Assert.Equal(ValueGenerated.Never, settings.FindProperty(nameof(SourcingSettings.Id))?.ValueGenerated);
        Assert.Equal(typeof(string), run.FindProperty(nameof(ImportRun.Status))?.GetProviderClrType());
        Assert.Equal(DatabaseFunctions.NewGuid, run.FindProperty(nameof(ImportRun.Id))?.GetDefaultValueSql());
        Assert.Equal(ValueGenerated.Never, syncState.FindProperty(nameof(SourcingSyncState.Source))?.ValueGenerated);

        var identityUser = dbContext.Model.FindEntityType(typeof(IdentityUser<Guid>));
        var identityRole = dbContext.Model.FindEntityType(typeof(IdentityRole<Guid>));
        Assert.Equal(DatabaseSchemas.Identity, identityUser?.GetSchema());
        Assert.Equal(DatabaseFunctions.NewGuid, identityUser?.FindProperty(nameof(IdentityUser<Guid>.Id))?.GetDefaultValueSql());
        Assert.Equal(DatabaseSchemas.Identity, identityRole?.GetSchema());
        Assert.Equal(DatabaseFunctions.NewGuid, identityRole?.FindProperty(nameof(IdentityRole<Guid>.Id))?.GetDefaultValueSql());
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
