using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RiposteOS.Core.Sourcing;

namespace RiposteOS.Infrastructure.Persistence.Configurations.Sourcing;

public sealed class SourcingSettingsEntityTypeConfiguration : IEntityTypeConfiguration<SourcingSettings>
{
    public void Configure(EntityTypeBuilder<SourcingSettings> builder)
    {
        builder.ToTable("sourcing_settings", DatabaseSchemas.Sourcing);
        builder.HasKey(settings => settings.Id);
        builder.Property(settings => settings.Id).ValueGeneratedNever();
        builder.Property(settings => settings.PageSize).IsRequired();
        builder.Property(settings => settings.PositiveSignalWeight).IsRequired();
        builder.Property(settings => settings.NegativeSignalPenalty).IsRequired();
        builder.Property(settings => settings.PreferredDepartmentBoost).IsRequired();
        builder.Property(settings => settings.CpvWhitelistBoost).IsRequired();
        builder.Property(settings => settings.CpvWatchBoost).IsRequired();
        builder.Property(settings => settings.CpvExclusionPenalty).IsRequired();
        builder.Property(settings => settings.UrgentDeadlineDays).IsRequired();
        builder.Property(settings => settings.UrgentDeadlinePenalty).IsRequired();
        builder.Property(settings => settings.HighRelevanceThreshold).IsRequired();
        builder.Property(settings => settings.BoampCron)
            .HasMaxLength(100)
            .HasDefaultValue(SourcingSettings.DefaultSynchronizationCron)
            .IsRequired();
        builder.Property(settings => settings.TedCron)
            .HasMaxLength(100)
            .HasDefaultValue(SourcingSettings.DefaultSynchronizationCron)
            .IsRequired();
        builder.Property(settings => settings.PlaceCron)
            .HasMaxLength(100)
            .HasDefaultValue(SourcingSettings.DefaultPlaceSynchronizationCron)
            .IsRequired();
        builder.Property(settings => settings.UpdatedAt)
            .HasDefaultValueSql(DatabaseFunctions.Now);

        builder.Ignore(settings => settings.Keywords);
        builder.Ignore(settings => settings.ExcludedKeywords);
        builder.Ignore(settings => settings.PositiveSignals);
        builder.Ignore(settings => settings.NegativeSignals);
        builder.Ignore(settings => settings.AllowedCountryCodes);
        builder.Ignore(settings => settings.PreferredDepartmentCodes);
        builder.Ignore(settings => settings.CpvWhitelistPrefixes);
        builder.Ignore(settings => settings.CpvWatchPrefixes);
        builder.Ignore(settings => settings.CpvExcludedPrefixes);
        builder.Property<string[]>("_keywords")
            .HasColumnName("Keywords")
            .IsRequired();
        builder.Property<string[]>("_excludedKeywords")
            .HasColumnName("ExcludedKeywords")
            .IsRequired();
        builder.Property<string[]>("_positiveSignals")
            .HasColumnName("PositiveSignals")
            .IsRequired();
        builder.Property<string[]>("_negativeSignals")
            .HasColumnName("NegativeSignals")
            .IsRequired();
        builder.Property<string[]>("_allowedCountryCodes")
            .HasColumnName("AllowedCountryCodes")
            .IsRequired();
        builder.Property<string[]>("_preferredDepartmentCodes")
            .HasColumnName("PreferredDepartmentCodes")
            .IsRequired();
        builder.Property<string[]>("_cpvWhitelistPrefixes")
            .HasColumnName("CpvWhitelistPrefixes")
            .IsRequired();
        builder.Property<string[]>("_cpvWatchPrefixes")
            .HasColumnName("CpvWatchPrefixes")
            .IsRequired();
        builder.Property<string[]>("_cpvExcludedPrefixes")
            .HasColumnName("CpvExcludedPrefixes")
            .IsRequired();
    }
}
