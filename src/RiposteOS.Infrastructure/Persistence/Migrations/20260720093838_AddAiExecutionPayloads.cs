using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RiposteOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAiExecutionPayloads : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ai_execution_payloads",
                schema: "ai",
                columns: table => new
                {
                    ExecutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Input = table.Column<string>(type: "jsonb", nullable: false),
                    Output = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_execution_payloads", x => x.ExecutionId);
                    table.ForeignKey(
                        name: "FK_ai_execution_payloads_ai_execution_logs_ExecutionId",
                        column: x => x.ExecutionId,
                        principalSchema: "ai",
                        principalTable: "ai_execution_logs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_execution_payloads",
                schema: "ai");
        }
    }
}
