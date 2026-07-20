using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RiposteOS.Core.Ai;

namespace RiposteOS.Infrastructure.Persistence.Configurations.Ai;

public sealed class AiExecutionLogEntityTypeConfiguration : IEntityTypeConfiguration<AiExecutionLog>
{
    public void Configure(EntityTypeBuilder<AiExecutionLog> builder)
    {
        builder.ToTable("ai_execution_logs", DatabaseSchemas.Ai);
        builder.HasKey(log => log.Id);
        builder.Property(log => log.Id).HasDefaultValueSql(DatabaseFunctions.NewGuid);
        builder.Property(log => log.Operation).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(log => log.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(log => log.SubjectKind).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(log => log.SubjectLabel).HasMaxLength(AiExecutionSubject.MaximumLabelLength).IsRequired();
        builder.Property(log => log.ProviderName).HasMaxLength(AiExecutionLog.MaximumProviderNameLength);
        builder.Property(log => log.Model).HasMaxLength(AiExecutionLog.MaximumModelLength);
        builder.Property(log => log.ErrorMessage).HasMaxLength(AiExecutionLog.MaximumErrorMessageLength);
        builder.HasIndex(log => log.StartedAt).HasDatabaseName("ix_ai_execution_logs_started_at");
        builder.HasIndex(log => log.CorrelationId).HasDatabaseName("ix_ai_execution_logs_correlation_id");
        builder.HasIndex(log => log.ProviderId).HasDatabaseName("ix_ai_execution_logs_provider_id");
    }
}
