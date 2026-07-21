using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RiposteOS.Infrastructure.Persistence.Migrations;

[DbContext(typeof(RiposteDbContext))]
[Migration("20260720140000_AddAssistantStructuredResponses")]
public partial class AddAssistantStructuredResponses : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "StructuredContent",
            schema: "consultations",
            table: "assistant_messages",
            type: "jsonb",
            maxLength: 16000,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "StructuredContent",
            schema: "consultations",
            table: "assistant_messages");
    }
}
