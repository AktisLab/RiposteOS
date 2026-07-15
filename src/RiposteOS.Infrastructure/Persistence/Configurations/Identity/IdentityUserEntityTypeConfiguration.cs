using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RiposteOS.Infrastructure.Persistence.Configurations.Identity;

public sealed class IdentityUserEntityTypeConfiguration : IEntityTypeConfiguration<IdentityUser<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityUser<Guid>> builder)
    {
        builder.ToTable("AspNetUsers", DatabaseSchemas.Identity);
        builder.Property(user => user.Id)
            .HasDefaultValueSql(DatabaseFunctions.NewGuid);
    }
}
