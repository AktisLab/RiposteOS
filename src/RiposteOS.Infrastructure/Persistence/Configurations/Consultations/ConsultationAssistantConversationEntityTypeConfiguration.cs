using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RiposteOS.Core.Consultations;

namespace RiposteOS.Infrastructure.Persistence.Configurations.Consultations;

public sealed class ConsultationAssistantConversationEntityTypeConfiguration : IEntityTypeConfiguration<ConsultationAssistantConversation>
{
    public void Configure(EntityTypeBuilder<ConsultationAssistantConversation> builder)
    {
        builder.ToTable("assistant_conversations", DatabaseSchemas.Consultations);
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasDefaultValueSql(DatabaseFunctions.NewGuid);
        builder.Property(item => item.Title).HasMaxLength(ConsultationAssistantConversation.MaximumTitleLength).IsRequired();
        builder.Property(item => item.CreatedAt).HasDefaultValueSql(DatabaseFunctions.Now);
        builder.Property(item => item.UpdatedAt).HasDefaultValueSql(DatabaseFunctions.Now);
        builder.HasIndex(item => new { item.ConsultationId, item.ArchivedAt, item.UpdatedAt, item.Id }).HasDatabaseName("ix_assistant_conversations_consultation_active_updated_id");
        builder.HasOne<Consultation>().WithMany().HasForeignKey(item => item.ConsultationId).OnDelete(DeleteBehavior.Cascade);
    }
}
