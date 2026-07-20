using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RiposteOS.Core.Ai;
using RiposteOS.Core.Consultations;
using RiposteOS.Core.Documents;

namespace RiposteOS.Infrastructure.Persistence.Configurations.Ai;

public sealed class ConsultationDocumentClassificationEntityTypeConfiguration : IEntityTypeConfiguration<ConsultationDocumentClassification>
{
    public void Configure(EntityTypeBuilder<ConsultationDocumentClassification> builder)
    {
        builder.ToTable("consultation_document_classifications", DatabaseSchemas.Ai); builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasDefaultValueSql(DatabaseFunctions.NewGuid);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(x => x.ProposedKind).HasConversion<string>().HasMaxLength(64);
        builder.Property(x => x.Confidence).HasConversion<string>().HasMaxLength(16);
        builder.Property(x => x.ProviderName).HasMaxLength(200); builder.Property(x => x.Model).HasMaxLength(200); builder.Property(x => x.ErrorMessage).HasMaxLength(1000);
        builder.Property(x => x.ProviderId);
        builder.Property<List<Guid>>("evidencePassageIds").HasColumnName("evidence_passage_ids").HasColumnType("uuid[]");
        builder.HasIndex(x => new { x.ConsultationId, x.StoredDocumentId }).IsUnique().HasDatabaseName("ux_ai_document_classifications_consultation_document");
        builder.HasOne<Consultation>().WithMany().HasForeignKey(x => x.ConsultationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<StoredDocument>().WithMany().HasForeignKey(x => x.StoredDocumentId).OnDelete(DeleteBehavior.Restrict);
    }
}
