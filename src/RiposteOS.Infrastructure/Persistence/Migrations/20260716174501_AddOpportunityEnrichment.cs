using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RiposteOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOpportunityEnrichment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContractNature",
                schema: "sourcing",
                table: "opportunities",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                schema: "sourcing",
                table: "opportunities",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                schema: "sourcing",
                table: "opportunities",
                type: "character varying(20000)",
                maxLength: 20000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DocumentUrl",
                schema: "sourcing",
                table: "opportunities",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "EstimatedValue",
                schema: "sourcing",
                table: "opportunities",
                type: "numeric(19,4)",
                precision: 19,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExecutionDuration",
                schema: "sourcing",
                table: "opportunities",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProcedureType",
                schema: "sourcing",
                table: "opportunities",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE sourcing.sourcing_sync_states AS state
                SET "LastSuccessfulPublicationDate" = (
                        SELECT MIN(opportunity."PublicationDate")
                        FROM sourcing.opportunities AS opportunity
                        WHERE opportunity."Source" = state."Source"
                    ),
                    "UpdatedAt" = NULL
                WHERE state."Source" IN ('BOAMP', 'TED');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContractNature",
                schema: "sourcing",
                table: "opportunities");

            migrationBuilder.DropColumn(
                name: "Currency",
                schema: "sourcing",
                table: "opportunities");

            migrationBuilder.DropColumn(
                name: "Description",
                schema: "sourcing",
                table: "opportunities");

            migrationBuilder.DropColumn(
                name: "DocumentUrl",
                schema: "sourcing",
                table: "opportunities");

            migrationBuilder.DropColumn(
                name: "EstimatedValue",
                schema: "sourcing",
                table: "opportunities");

            migrationBuilder.DropColumn(
                name: "ExecutionDuration",
                schema: "sourcing",
                table: "opportunities");

            migrationBuilder.DropColumn(
                name: "ProcedureType",
                schema: "sourcing",
                table: "opportunities");
        }
    }
}
