using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RiposteOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUnchangedImportCountAndFrenchDefault : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Unchanged",
                schema: "sourcing",
                table: "import_runs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(
                """
                UPDATE sourcing.sourcing_settings
                SET "AllowedCountryCodes" = ARRAY['FRA']::text[]
                WHERE "Id" = 1 AND cardinality("AllowedCountryCodes") = 0;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Unchanged",
                schema: "sourcing",
                table: "import_runs");
        }
    }
}
