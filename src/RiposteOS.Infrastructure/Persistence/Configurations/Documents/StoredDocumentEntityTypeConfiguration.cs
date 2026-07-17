using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RiposteOS.Core.Documents;

namespace RiposteOS.Infrastructure.Persistence.Configurations.Documents;

public sealed class StoredDocumentEntityTypeConfiguration : IEntityTypeConfiguration<StoredDocument>
{
    public void Configure(EntityTypeBuilder<StoredDocument> builder)
    {
        builder.ToTable("stored_documents", DatabaseSchemas.Documents);
        builder.HasKey(document => document.Id);
        builder.Property(document => document.Id).HasDefaultValueSql(DatabaseFunctions.NewGuid);
        builder.Property(document => document.OriginalFileName).HasMaxLength(255).IsRequired();
        builder.Property(document => document.ContentType).HasMaxLength(255).IsRequired();
        builder.Property(document => document.Size).IsRequired();
        builder.Property(document => document.Sha256).HasMaxLength(64).IsRequired();
        builder.Property(document => document.StorageKey).HasMaxLength(128).IsRequired();
        builder.Property(document => document.CreatedAt).HasDefaultValueSql(DatabaseFunctions.Now);
        builder.HasIndex(document => document.StorageKey).IsUnique().HasDatabaseName("ux_stored_documents_storage_key");
        builder.HasIndex(document => new { document.CreatedAt, document.Id }).HasDatabaseName("ix_stored_documents_created_at_id");
    }
}
