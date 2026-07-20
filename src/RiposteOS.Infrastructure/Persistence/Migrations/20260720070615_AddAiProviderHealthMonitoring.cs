using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RiposteOS.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddAiProviderHealthMonitoring : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "HealthCheckedAt",
            schema: "ai",
            table: "providers",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "HealthStatus",
            schema: "ai",
            table: "providers",
            type: "character varying(32)",
            maxLength: 32,
            nullable: false,
            defaultValue: "Unknown");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "HealthCheckedAt",
            schema: "ai",
            table: "providers");

        migrationBuilder.DropColumn(
            name: "HealthStatus",
            schema: "ai",
            table: "providers");
    }
}
