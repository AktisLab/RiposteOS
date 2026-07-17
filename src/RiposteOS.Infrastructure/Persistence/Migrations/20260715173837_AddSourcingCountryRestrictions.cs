using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RiposteOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSourcingCountryRestrictions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string[]>(
                name: "AllowedCountryCodes",
                schema: "sourcing",
                table: "sourcing_settings",
                type: "text[]",
                nullable: false,
                defaultValue: Array.Empty<string>());
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AllowedCountryCodes",
                schema: "sourcing",
                table: "sourcing_settings");
        }
    }
}
