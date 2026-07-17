using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RiposteOS.Core.Sourcing;

namespace RiposteOS.Infrastructure.Persistence.Configurations.Sourcing;

public sealed class SourcingSyncStateEntityTypeConfiguration : IEntityTypeConfiguration<SourcingSyncState>
{
    public void Configure(EntityTypeBuilder<SourcingSyncState> builder)
    {
        builder.ToTable("sourcing_sync_states", DatabaseSchemas.Sourcing);
        builder.HasKey(state => state.Source);
        builder.Property(state => state.Source)
            .HasMaxLength(32)
            .IsRequired()
            .ValueGeneratedNever();
    }
}
