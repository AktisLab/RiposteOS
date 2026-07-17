using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RiposteOS.Core.Sourcing;

namespace RiposteOS.Infrastructure.Persistence.Configurations.Sourcing;

public sealed class OpportunityEntityTypeConfiguration : IEntityTypeConfiguration<Opportunity>
{
    public void Configure(EntityTypeBuilder<Opportunity> builder)
    {
        builder.ToTable("opportunities", DatabaseSchemas.Sourcing);
        builder.HasKey(opportunity => opportunity.Id);
        builder.Property(opportunity => opportunity.Id)
            .HasDefaultValueSql(DatabaseFunctions.NewGuid);
        builder.HasIndex(opportunity => new { opportunity.Source, opportunity.SourceId })
            .IsUnique()
            .HasDatabaseName("ix_opportunities_source_source_id");
        builder.HasIndex(opportunity => opportunity.EformsNoticeId)
            .IsUnique()
            .HasFilter("\"EformsNoticeId\" IS NOT NULL")
            .HasDatabaseName("ix_opportunities_eforms_notice_id");
        builder.Property(opportunity => opportunity.Source).HasMaxLength(32).IsRequired();
        builder.Property(opportunity => opportunity.SourceId).HasMaxLength(64).IsRequired();
        builder.Property(opportunity => opportunity.Title).HasMaxLength(2_000).IsRequired();
        builder.Property(opportunity => opportunity.Buyer).HasMaxLength(1_000).IsRequired();
        builder.Property(opportunity => opportunity.Description).HasMaxLength(20_000);
        builder.Property(opportunity => opportunity.ProcedureType).HasMaxLength(128);
        builder.Property(opportunity => opportunity.ContractNature).HasMaxLength(128);
        builder.Property(opportunity => opportunity.EstimatedValue).HasPrecision(19, 4);
        builder.Property(opportunity => opportunity.Currency).HasMaxLength(3);
        builder.Property(opportunity => opportunity.ExecutionDuration).HasColumnType("text");
        builder.Property(opportunity => opportunity.DocumentUrl).HasMaxLength(2_000);
        builder.Property(opportunity => opportunity.MatchScore).IsRequired();
        builder.HasIndex(opportunity => opportunity.MatchScore)
            .HasDatabaseName("ix_opportunities_match_score");
        builder.Property(opportunity => opportunity.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();
        builder.HasIndex(opportunity => opportunity.Status)
            .HasDatabaseName("ix_opportunities_status");
        builder.Property(opportunity => opportunity.NoticeUrl).HasMaxLength(2_000).IsRequired();
        builder.Property(opportunity => opportunity.RawPayload).HasColumnType("jsonb").IsRequired();
        builder.Property(opportunity => opportunity.ContentHash).HasMaxLength(64).IsRequired();
        builder.Property(opportunity => opportunity.ImportedAt)
            .HasDefaultValueSql(DatabaseFunctions.Now);
        builder.Property(opportunity => opportunity.UpdatedAt)
            .HasDefaultValueSql(DatabaseFunctions.Now);

        builder.Ignore(opportunity => opportunity.CountryCodes);
        builder.Ignore(opportunity => opportunity.DepartmentCodes);
        builder.Ignore(opportunity => opportunity.CpvCodes);
        builder.Ignore(opportunity => opportunity.DescriptorCodes);
        builder.Ignore(opportunity => opportunity.DescriptorLabels);
        builder.Ignore(opportunity => opportunity.MatchReasons);
        builder.Property<string[]>("_countryCodes")
            .HasColumnName("CountryCodes")
            .IsRequired();
        builder.Property<string[]>("_departmentCodes")
            .HasColumnName("DepartmentCodes")
            .IsRequired();
        builder.Property<string[]>("_cpvCodes")
            .HasColumnName("CpvCodes")
            .IsRequired();
        builder.Property<string[]>("_descriptorCodes")
            .HasColumnName("DescriptorCodes")
            .IsRequired();
        builder.Property<string[]>("_descriptorLabels")
            .HasColumnName("DescriptorLabels")
            .IsRequired();
        builder.Property<string[]>("_matchReasons")
            .HasColumnName("MatchReasons")
            .IsRequired();
    }
}
