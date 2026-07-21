using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Pgvector;
using RiposteOS.Core.Documents;

namespace RiposteOS.Infrastructure.Persistence.Configurations.Documents;

public sealed class DocumentPassageEmbeddingEntityTypeConfiguration : IEntityTypeConfiguration<DocumentPassageEmbedding>
{
    public void Configure(EntityTypeBuilder<DocumentPassageEmbedding> builder)
    {
        builder.ToTable("document_passage_embeddings", DatabaseSchemas.Ai);
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasDefaultValueSql(DatabaseFunctions.NewGuid);
        builder.Property(item => item.DocumentPassageId).IsRequired();
        builder.Property(item => item.TextHash).HasMaxLength(64).IsRequired();
        builder.Property(item => item.ProviderName).HasMaxLength(200).IsRequired();
        builder.Property(item => item.Model).HasMaxLength(200).IsRequired();
        builder.Property(item => item.Dimension).IsRequired();
        builder.Property(item => item.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(item => item.ErrorMessage).HasMaxLength(500);
        builder.Property(item => item.QueuedAt).HasDefaultValueSql(DatabaseFunctions.Now);
        builder.Property(item => item.Embedding)
            .HasConversion(new ValueConverter<float[]?, Vector?>(
                value => value == null ? null : new Vector(value),
                value => value == null ? null : value.ToArray()))
            .Metadata.SetValueComparer(new ValueComparer<float[]?>(
                (left, right) => left == right || (left != null && right != null && left.SequenceEqual(right)),
                value => value == null ? 0 : value.Aggregate(0, (hash, item) => HashCode.Combine(hash, item.GetHashCode())),
                value => value == null ? null : value.ToArray()));
        builder.Property(item => item.Embedding).HasColumnType("vector(1024)");
        builder.HasIndex(item => item.DocumentPassageId).IsUnique().HasDatabaseName("ux_document_passage_embeddings_passage_id");
        builder.HasIndex(item => item.Status).HasDatabaseName("ix_document_passage_embeddings_status");
        builder.HasIndex(item => item.Embedding).HasMethod("hnsw").HasOperators("vector_cosine_ops").HasDatabaseName("ix_document_passage_embeddings_embedding_cosine");
        builder.HasOne<DocumentPassage>().WithMany().HasForeignKey(item => item.DocumentPassageId).OnDelete(DeleteBehavior.Cascade);
    }
}
