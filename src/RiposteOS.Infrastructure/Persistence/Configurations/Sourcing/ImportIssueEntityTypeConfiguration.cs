using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RiposteOS.Core.Sourcing;

namespace RiposteOS.Infrastructure.Persistence.Configurations.Sourcing;

public sealed class ImportIssueEntityTypeConfiguration : IEntityTypeConfiguration<ImportIssue>
{
    public void Configure(EntityTypeBuilder<ImportIssue> builder)
    {
        builder.ToTable("import_issues", DatabaseSchemas.Sourcing);
        builder.HasKey(issue => issue.Id);
        builder.Property(issue => issue.Id).HasDefaultValueSql(DatabaseFunctions.NewGuid);
        builder.Property(issue => issue.Source).HasMaxLength(32).IsRequired();
        builder.Property(issue => issue.SourceId).HasMaxLength(128);
        builder.Property(issue => issue.ErrorCode).HasMaxLength(64).IsRequired();
        builder.Property(issue => issue.RawPayload).HasColumnType("jsonb").IsRequired();
        builder.Property(issue => issue.CreatedAt).HasDefaultValueSql(DatabaseFunctions.Now);
        builder.HasIndex(issue => issue.RunId).HasDatabaseName("ix_import_issues_run_id");
        builder.HasOne<ImportRun>()
            .WithMany()
            .HasForeignKey(issue => issue.RunId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
