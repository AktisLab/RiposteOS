using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RiposteOS.Core.Ai;

namespace RiposteOS.Infrastructure.Persistence.Configurations.Ai;

public sealed class AiExecutionPayloadEntityTypeConfiguration : IEntityTypeConfiguration<AiExecutionPayload>
{
    public void Configure(EntityTypeBuilder<AiExecutionPayload> builder)
    {
        builder.ToTable("ai_execution_payloads", DatabaseSchemas.Ai);
        builder.HasKey(payload => payload.ExecutionId);
        builder.Property(payload => payload.Input).HasColumnType("jsonb").IsRequired();
        builder.Property(payload => payload.Output).HasColumnType("jsonb");
        builder.HasOne<AiExecutionLog>()
            .WithOne()
            .HasForeignKey<AiExecutionPayload>(payload => payload.ExecutionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
