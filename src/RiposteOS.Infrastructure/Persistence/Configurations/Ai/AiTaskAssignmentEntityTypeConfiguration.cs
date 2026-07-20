using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RiposteOS.Core.Ai;

namespace RiposteOS.Infrastructure.Persistence.Configurations.Ai;

public sealed class AiTaskAssignmentEntityTypeConfiguration : IEntityTypeConfiguration<AiTaskAssignment>
{
    public void Configure(EntityTypeBuilder<AiTaskAssignment> builder)
    {
        builder.ToTable("task_assignments", DatabaseSchemas.Ai); builder.HasKey(x => x.Task);
        builder.Property(x => x.Task).HasConversion<string>().HasMaxLength(64);
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql(DatabaseFunctions.Now);
        builder.HasOne<AiProvider>().WithMany().HasForeignKey(x => x.ProviderId).OnDelete(DeleteBehavior.Restrict);
    }
}
