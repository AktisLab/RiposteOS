using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RiposteOS.Core.Consultations;
using RiposteOS.Core.Documents;

namespace RiposteOS.Infrastructure.Persistence.Configurations.Consultations;

public sealed class ConsultationAssistantMessageCitationEntityTypeConfiguration : IEntityTypeConfiguration<ConsultationAssistantMessageCitation>
{
    public void Configure(EntityTypeBuilder<ConsultationAssistantMessageCitation> builder)
    {
        builder.ToTable("assistant_message_citations", DatabaseSchemas.Consultations);
        builder.HasKey(item => new { item.MessageId, item.DocumentPassageId });
        builder.HasIndex(item => item.DocumentPassageId).HasDatabaseName("ix_assistant_message_citations_passage_id");
        builder.HasOne<ConsultationAssistantMessage>().WithMany().HasForeignKey(item => item.MessageId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<DocumentPassage>().WithMany().HasForeignKey(item => item.DocumentPassageId).OnDelete(DeleteBehavior.Restrict);
    }
}
