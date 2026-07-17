using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RiposteOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddConfigurableSourcingSchedules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BoampCron",
                schema: "sourcing",
                table: "sourcing_settings",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "0 * * * *");

            migrationBuilder.AddColumn<string>(
                name: "TedCron",
                schema: "sourcing",
                table: "sourcing_settings",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "0 * * * *");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BoampCron",
                schema: "sourcing",
                table: "sourcing_settings");

            migrationBuilder.DropColumn(
                name: "TedCron",
                schema: "sourcing",
                table: "sourcing_settings");
        }
    }
}
