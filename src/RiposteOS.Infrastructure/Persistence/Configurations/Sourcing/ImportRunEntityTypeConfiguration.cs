using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RiposteOS.Core.Sourcing;

namespace RiposteOS.Infrastructure.Persistence.Configurations.Sourcing;

public sealed class ImportRunEntityTypeConfiguration : IEntityTypeConfiguration<ImportRun>
{
    public void Configure(EntityTypeBuilder<ImportRun> builder)
    {
        builder.ToTable("import_runs", DatabaseSchemas.Sourcing);
        builder.HasKey(run => run.Id);
        builder.Property(run => run.Id)
            .HasDefaultValueSql(DatabaseFunctions.NewGuid);
        builder.Property(run => run.Source).HasMaxLength(32).IsRequired();
        builder.Property(run => run.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(run => run.ErrorMessage).HasMaxLength(1_000);
        builder.Property(run => run.QueuedAt)
            .HasDefaultValueSql(DatabaseFunctions.Now);
        builder.Property(run => run.LastHeartbeatAt)
            .HasDefaultValueSql(DatabaseFunctions.Now);
        builder.HasIndex(run => run.QueuedAt)
            .HasDatabaseName("ix_import_runs_queued_at");
        builder.HasIndex(run => run.Source)
            .IsUnique()
            .HasFilter("\"Status\" IN ('Queued', 'Running')")
            .HasDatabaseName("ix_import_runs_active_source");
    }
}
