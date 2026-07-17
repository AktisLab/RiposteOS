using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RiposteOS.Core.Sourcing;

namespace RiposteOS.Infrastructure.Persistence.Configurations.Sourcing;

public sealed class OpportunityPublicationEntityTypeConfiguration
    : IEntityTypeConfiguration<OpportunityPublication>
{
    public void Configure(EntityTypeBuilder<OpportunityPublication> builder)
    {
        builder.ToTable("opportunity_publications", DatabaseSchemas.Sourcing);
        builder.HasKey(publication => publication.Id);
        builder.Property(publication => publication.Id)
            .HasDefaultValueSql(DatabaseFunctions.NewGuid);
        builder.Property(publication => publication.Source).HasMaxLength(32).IsRequired();
        builder.Property(publication => publication.SourceId).HasMaxLength(64).IsRequired();
        builder.Property(publication => publication.NoticeUrl).HasMaxLength(2_000).IsRequired();
        builder.Property(publication => publication.DocumentUrl).HasMaxLength(2_000).IsRequired();
        builder.Property(publication => publication.RawPayload).HasColumnType("jsonb").IsRequired();
        builder.Property(publication => publication.ContentHash).HasMaxLength(64).IsRequired();
        builder.Property(publication => publication.FirstSeenAt).IsRequired();
        builder.Property(publication => publication.UpdatedAt).IsRequired();
        builder.HasIndex(publication => new { publication.Source, publication.SourceId })
            .IsUnique()
            .HasDatabaseName("ix_opportunity_publications_source_source_id");
        builder.HasIndex(publication => publication.OpportunityId)
            .HasDatabaseName("ix_opportunity_publications_opportunity_id");
        builder.HasOne(publication => publication.Opportunity)
            .WithMany(opportunity => opportunity.Publications)
            .HasForeignKey(publication => publication.OpportunityId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
