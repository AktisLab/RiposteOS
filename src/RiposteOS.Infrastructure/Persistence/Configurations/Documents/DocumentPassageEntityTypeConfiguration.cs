using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RiposteOS.Core.Documents;

namespace RiposteOS.Infrastructure.Persistence.Configurations.Documents;

public sealed class DocumentPassageEntityTypeConfiguration
    : IEntityTypeConfiguration<DocumentPassage>
{
    public void Configure(EntityTypeBuilder<DocumentPassage> builder)
    {
        builder.ToTable("document_passages", DatabaseSchemas.Documents);
        builder.HasKey(passage => passage.Id);
        builder.Property(passage => passage.Id).HasDefaultValueSql(DatabaseFunctions.NewGuid);
        builder.Property(passage => passage.Text).IsRequired();
        builder.Property(passage => passage.SectionTitle).HasMaxLength(DocumentPassage.MaximumSectionTitleLength);
        builder.Property(passage => passage.SourceLocation).HasMaxLength(DocumentPassage.MaximumSourceLocationLength);
        builder.HasIndex(passage => new { passage.DocumentProcessingRunId, passage.Ordinal })
            .IsUnique()
            .HasDatabaseName("ux_document_passages_run_ordinal");
        builder.HasOne(passage => passage.DocumentProcessingRun)
            .WithMany()
            .HasForeignKey(passage => passage.DocumentProcessingRunId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
