using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RiposteOS.Core.Documents;

namespace RiposteOS.Infrastructure.Persistence.Configurations.Documents;

public sealed class DocumentProcessingRunEntityTypeConfiguration
    : IEntityTypeConfiguration<DocumentProcessingRun>
{
    public void Configure(EntityTypeBuilder<DocumentProcessingRun> builder)
    {
        builder.ToTable("document_processing_runs", DatabaseSchemas.Documents);
        builder.HasKey(run => run.Id);
        builder.Property(run => run.Id).HasDefaultValueSql(DatabaseFunctions.NewGuid);
        builder.Property(run => run.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(run => run.ErrorMessage).HasMaxLength(DocumentProcessingRun.MaximumErrorMessageLength);
        builder.Property(run => run.QueuedAt).HasDefaultValueSql(DatabaseFunctions.Now);
        builder.HasIndex(run => run.StoredDocumentId)
            .IsUnique()
            .HasDatabaseName("ux_document_processing_runs_stored_document_id");
        builder.HasOne(run => run.StoredDocument)
            .WithMany()
            .HasForeignKey(run => run.StoredDocumentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
