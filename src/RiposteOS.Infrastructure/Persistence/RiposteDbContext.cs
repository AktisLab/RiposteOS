using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
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
    }
}
