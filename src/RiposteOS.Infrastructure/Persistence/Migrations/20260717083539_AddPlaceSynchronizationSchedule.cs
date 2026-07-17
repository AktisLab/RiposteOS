using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RiposteOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPlaceSynchronizationSchedule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PlaceCron",
                schema: "sourcing",
                table: "sourcing_settings",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "0 6,18 * * *");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PlaceCron",
                schema: "sourcing",
                table: "sourcing_settings");
        }
    }
}
