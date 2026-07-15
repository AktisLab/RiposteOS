using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RiposteOS.Infrastructure.Persistence.Configurations.Identity;

public sealed class IdentityRoleEntityTypeConfiguration : IEntityTypeConfiguration<IdentityRole<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityRole<Guid>> builder)
    {
        builder.ToTable("AspNetRoles", DatabaseSchemas.Identity);
        builder.Property(role => role.Id)
            .HasDefaultValueSql(DatabaseFunctions.NewGuid);
    }
}
