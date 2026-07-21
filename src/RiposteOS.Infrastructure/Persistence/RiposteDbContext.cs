using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using RiposteOS.Core.Documents;
using RiposteOS.Infrastructure.Persistence.Configurations;

namespace RiposteOS.Infrastructure.Persistence;

public sealed class RiposteDbContext(DbContextOptions<RiposteDbContext> options)
    : IdentityDbContext<IdentityUser<Guid>, IdentityRole<Guid>, Guid>(options)
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.HasDefaultSchema(DatabaseSchemas.Identity);
        base.OnModelCreating(builder);
        builder.HasPostgresExtension("vector");
        builder.ApplyConfigurationsFromAssembly(typeof(RiposteDbContext).Assembly);
        if (Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory")
        {
            var embedding = builder.Entity<DocumentPassageEmbedding>().Property(item => item.Embedding);
            embedding.HasConversion((ValueConverter?)null);
            embedding.Metadata.SetColumnType(null);
        }
    }
}
