using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RiposteOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStoredAiProviderApiKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EncryptedApiKey",
                schema: "ai",
                table: "providers",
                type: "character varying(8000)",
                maxLength: 8000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EncryptedApiKey",
                schema: "ai",
                table: "providers");
        }
    }
}
