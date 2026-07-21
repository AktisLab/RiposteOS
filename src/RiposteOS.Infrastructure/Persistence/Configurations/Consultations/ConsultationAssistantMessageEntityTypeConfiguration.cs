using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RiposteOS.Core.Consultations;

namespace RiposteOS.Infrastructure.Persistence.Configurations.Consultations;

public sealed class ConsultationAssistantMessageEntityTypeConfiguration : IEntityTypeConfiguration<ConsultationAssistantMessage>
{
    public void Configure(EntityTypeBuilder<ConsultationAssistantMessage> builder)
    {
        builder.ToTable("assistant_messages", DatabaseSchemas.Consultations);
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasDefaultValueSql(DatabaseFunctions.NewGuid);
        builder.Property(item => item.Role).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(item => item.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(item => item.Content).HasMaxLength(ConsultationAssistantMessage.MaximumContentLength);
        builder.Property(item => item.ProviderName).HasMaxLength(200);
        builder.Property(item => item.Model).HasMaxLength(200);
        builder.Property(item => item.ErrorMessage).HasMaxLength(ConsultationAssistantMessage.MaximumErrorLength);
        builder.Property(item => item.StructuredContent).HasColumnType("jsonb").HasMaxLength(ConsultationAssistantMessage.MaximumContentLength);
        builder.Property(item => item.CreatedAt).HasDefaultValueSql(DatabaseFunctions.Now);
        builder.HasIndex(item => new { item.ConversationId, item.CreatedAt, item.Id }).HasDatabaseName("ix_assistant_messages_conversation_created_id");
        builder.HasOne<ConsultationAssistantConversation>().WithMany().HasForeignKey(item => item.ConversationId).OnDelete(DeleteBehavior.Cascade);
    }
}
