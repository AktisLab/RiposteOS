using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RiposteOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOpportunityChangeHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContentHash",
                schema: "sourcing",
                table: "opportunities",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "import_issues",
                schema: "sourcing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    Source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SourceId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ErrorCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RawPayload = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_import_issues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_import_issues_import_runs_RunId",
                        column: x => x.RunId,
                        principalSchema: "sourcing",
                        principalTable: "import_runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "opportunity_revisions",
                schema: "sourcing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    OpportunityId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RawPayload = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_opportunity_revisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_opportunity_revisions_opportunities_OpportunityId",
                        column: x => x.OpportunityId,
                        principalSchema: "sourcing",
                        principalTable: "opportunities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_import_issues_run_id",
                schema: "sourcing",
                table: "import_issues",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "ix_opportunity_revisions_opportunity_created_at",
                schema: "sourcing",
                table: "opportunity_revisions",
                columns: ["OpportunityId", "CreatedAt"]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "import_issues",
                schema: "sourcing");

            migrationBuilder.DropTable(
                name: "opportunity_revisions",
                schema: "sourcing");

            migrationBuilder.DropColumn(
                name: "ContentHash",
                schema: "sourcing",
                table: "opportunities");
        }
    }
}
