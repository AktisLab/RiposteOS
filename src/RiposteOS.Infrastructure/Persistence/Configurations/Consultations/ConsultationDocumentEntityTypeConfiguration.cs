using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RiposteOS.Core.Consultations;
using RiposteOS.Core.Documents;

namespace RiposteOS.Infrastructure.Persistence.Configurations.Consultations;

public sealed class ConsultationDocumentEntityTypeConfiguration
    : IEntityTypeConfiguration<ConsultationDocument>
{
    public void Configure(EntityTypeBuilder<ConsultationDocument> builder)
    {
        builder.ToTable("consultation_documents", DatabaseSchemas.Consultations);
        builder.HasKey(document => new { document.ConsultationId, document.StoredDocumentId });
        builder.Property(document => document.Kind)
            .HasConversion<string>()
            .HasMaxLength(64)
            .IsRequired();
        builder.Property(document => document.AddedAt)
            .HasDefaultValueSql(DatabaseFunctions.Now);
        builder.HasIndex(document => new
        {
            document.ConsultationId,
            document.AddedAt,
            document.StoredDocumentId,
        })
            .HasDatabaseName("ix_consultation_documents_consultation_added_at_id");
        builder.HasIndex(document => document.StoredDocumentId)
            .HasDatabaseName("ix_consultation_documents_stored_document_id");
        builder.HasOne<Consultation>()
            .WithMany()
            .HasForeignKey(document => document.ConsultationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<StoredDocument>()
            .WithMany()
            .HasForeignKey(document => document.StoredDocumentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
