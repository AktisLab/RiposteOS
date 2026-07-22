using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RiposteOS.Core.Ai;

namespace RiposteOS.Infrastructure.Persistence.Configurations.Ai;

public sealed class AiProviderEntityTypeConfiguration : IEntityTypeConfiguration<AiProvider>
{
    public void Configure(EntityTypeBuilder<AiProvider> builder)
    {
        builder.ToTable("providers", DatabaseSchemas.Ai); builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasDefaultValueSql(DatabaseFunctions.NewGuid);
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Protocol).HasConversion<string>().HasMaxLength(64).IsRequired();
        builder.Property(x => x.BaseUrl).HasMaxLength(2_000).IsRequired();
        builder.Property(x => x.Model).HasMaxLength(200).IsRequired();
        builder.Property(x => x.ApiKeyEnvironmentVariableName).HasMaxLength(200);
        builder.Property(x => x.EncryptedApiKey).HasMaxLength(8_000);
        builder.Ignore(x => x.HasStoredApiKey);
        builder.Property(x => x.Capabilities).HasConversion<string>().HasMaxLength(64).HasDefaultValue(AiProviderCapabilities.Chat).IsRequired();
        builder.Property(x => x.HealthStatus).HasConversion<string>().HasMaxLength(32).HasDefaultValue(AiProviderHealthStatus.Unknown).IsRequired();
        builder.Property(x => x.HealthCheckedAt);
        builder.Property(x => x.CreatedAt).HasDefaultValueSql(DatabaseFunctions.Now);
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql(DatabaseFunctions.Now);
        builder.HasIndex(x => x.Name).IsUnique().HasDatabaseName("ux_ai_providers_name");
    }
}
