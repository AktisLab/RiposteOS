using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RiposteOS.Core.Sourcing;

namespace RiposteOS.Infrastructure.Persistence.Configurations.Sourcing;

public sealed class OpportunityRevisionEntityTypeConfiguration
    : IEntityTypeConfiguration<OpportunityRevision>
{
    public void Configure(EntityTypeBuilder<OpportunityRevision> builder)
    {
        builder.ToTable("opportunity_revisions", DatabaseSchemas.Sourcing);
        builder.HasKey(revision => revision.Id);
        builder.Property(revision => revision.Id)
            .HasDefaultValueSql(DatabaseFunctions.NewGuid);
        builder.Property(revision => revision.ContentHash).HasMaxLength(64).IsRequired();
        builder.Property(revision => revision.RawPayload).HasColumnType("jsonb").IsRequired();
        builder.Property(revision => revision.CreatedAt).IsRequired();
        builder.HasOne(revision => revision.Opportunity)
            .WithMany()
            .HasForeignKey(revision => revision.OpportunityId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(revision => new { revision.OpportunityId, revision.CreatedAt })
            .HasDatabaseName("ix_opportunity_revisions_opportunity_created_at");
    }
}
