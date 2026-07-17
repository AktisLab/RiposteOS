using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RiposteOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOpportunityPublications : Migration
    {
        private static readonly string[] PublicationIdentityColumns = ["Source", "SourceId"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "opportunity_publications",
                schema: "sourcing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    OpportunityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SourceId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    NoticeUrl = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    DocumentUrl = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    RawPayload = table.Column<string>(type: "jsonb", nullable: false),
                    ContentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    FirstSeenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_opportunity_publications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_opportunity_publications_opportunities_OpportunityId",
                        column: x => x.OpportunityId,
                        principalSchema: "sourcing",
                        principalTable: "opportunities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql(
                """
                INSERT INTO sourcing.opportunity_publications
                    ("Id", "OpportunityId", "Source", "SourceId", "NoticeUrl", "DocumentUrl",
                     "RawPayload", "ContentHash", "FirstSeenAt", "UpdatedAt")
                SELECT gen_random_uuid(), "Id", "Source", "SourceId", "NoticeUrl",
                       COALESCE("DocumentUrl", ''), "RawPayload", "ContentHash", "ImportedAt", "UpdatedAt"
                FROM sourcing.opportunities
                WHERE "Source" IN ('BOAMP', 'TED')
                """);

            migrationBuilder.CreateIndex(
                name: "ix_opportunity_publications_opportunity_id",
                schema: "sourcing",
                table: "opportunity_publications",
                column: "OpportunityId");

            migrationBuilder.CreateIndex(
                name: "ix_opportunity_publications_source_source_id",
                schema: "sourcing",
                table: "opportunity_publications",
                columns: PublicationIdentityColumns,
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "opportunity_publications",
                schema: "sourcing");
        }
    }
}
