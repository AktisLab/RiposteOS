using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RiposteOS.Core.Consultations;
using RiposteOS.Core.Sourcing;

namespace RiposteOS.Infrastructure.Persistence.Configurations.Consultations;

public sealed class ConsultationEntityTypeConfiguration : IEntityTypeConfiguration<Consultation>
{
    public void Configure(EntityTypeBuilder<Consultation> builder)
    {
        builder.ToTable("consultations", DatabaseSchemas.Consultations);
        builder.HasKey(consultation => consultation.Id);
        builder.Property(consultation => consultation.Id)
            .HasDefaultValueSql(DatabaseFunctions.NewGuid);
        builder.Property(consultation => consultation.Title)
            .HasMaxLength(Consultation.MaximumTitleLength)
            .IsRequired();
        builder.Property(consultation => consultation.Buyer)
            .HasMaxLength(Consultation.MaximumBuyerLength)
            .IsRequired();
        builder.Property(consultation => consultation.NoticeUrl)
            .HasMaxLength(Consultation.MaximumNoticeUrlLength);
        builder.Property(consultation => consultation.CreatedAt)
            .HasDefaultValueSql(DatabaseFunctions.Now);
        builder.Property(consultation => consultation.UpdatedAt)
            .HasDefaultValueSql(DatabaseFunctions.Now);
        builder.HasIndex(consultation => consultation.OpportunityId)
            .IsUnique()
            .HasFilter("\"OpportunityId\" IS NOT NULL")
            .HasDatabaseName("ux_consultations_opportunity_id");
        builder.HasIndex(consultation => new { consultation.ResponseDeadline, consultation.Id })
            .HasDatabaseName("ix_consultations_response_deadline_id");
        builder.HasOne<Opportunity>()
            .WithOne()
            .HasForeignKey<Consultation>(consultation => consultation.OpportunityId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
