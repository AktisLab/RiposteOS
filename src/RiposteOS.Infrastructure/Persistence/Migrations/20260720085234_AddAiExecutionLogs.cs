using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RiposteOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAiExecutionLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ai_execution_logs",
                schema: "ai",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    Operation = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    StoredDocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    DocumentProcessingRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    DocumentClassificationId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProviderId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProviderName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Model = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FailedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_execution_logs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ai_execution_logs_classification_id",
                schema: "ai",
                table: "ai_execution_logs",
                column: "DocumentClassificationId");

            migrationBuilder.CreateIndex(
                name: "ix_ai_execution_logs_processing_run_id",
                schema: "ai",
                table: "ai_execution_logs",
                column: "DocumentProcessingRunId");

            migrationBuilder.CreateIndex(
                name: "ix_ai_execution_logs_provider_id",
                schema: "ai",
                table: "ai_execution_logs",
                column: "ProviderId");

            migrationBuilder.CreateIndex(
                name: "ix_ai_execution_logs_started_at",
                schema: "ai",
                table: "ai_execution_logs",
                column: "StartedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_execution_logs",
                schema: "ai");
        }
    }
}
