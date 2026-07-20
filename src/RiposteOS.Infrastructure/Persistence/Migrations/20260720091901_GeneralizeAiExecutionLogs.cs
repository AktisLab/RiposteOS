using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RiposteOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GeneralizeAiExecutionLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_ai_execution_logs_classification_id",
                schema: "ai",
                table: "ai_execution_logs");

            migrationBuilder.RenameColumn(
                name: "StoredDocumentId",
                schema: "ai",
                table: "ai_execution_logs",
                newName: "SubjectId");

            migrationBuilder.RenameColumn(
                name: "DocumentProcessingRunId",
                schema: "ai",
                table: "ai_execution_logs",
                newName: "CorrelationId");

            migrationBuilder.RenameColumn(
                name: "DocumentName",
                schema: "ai",
                table: "ai_execution_logs",
                newName: "SubjectLabel");

            migrationBuilder.RenameIndex(
                name: "ix_ai_execution_logs_processing_run_id",
                schema: "ai",
                table: "ai_execution_logs",
                newName: "ix_ai_execution_logs_correlation_id");

            migrationBuilder.AddColumn<string>(
                name: "SubjectKind",
                schema: "ai",
                table: "ai_execution_logs",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Document");

            migrationBuilder.Sql("""
                UPDATE ai.ai_execution_logs
                SET "CorrelationId" = COALESCE("CorrelationId", "DocumentClassificationId");
                """);

            migrationBuilder.DropColumn(
                name: "DocumentClassificationId",
                schema: "ai",
                table: "ai_execution_logs");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SubjectKind",
                schema: "ai",
                table: "ai_execution_logs");

            migrationBuilder.RenameColumn(
                name: "SubjectLabel",
                schema: "ai",
                table: "ai_execution_logs",
                newName: "DocumentName");

            migrationBuilder.RenameColumn(
                name: "SubjectId",
                schema: "ai",
                table: "ai_execution_logs",
                newName: "StoredDocumentId");

            migrationBuilder.RenameColumn(
                name: "CorrelationId",
                schema: "ai",
                table: "ai_execution_logs",
                newName: "DocumentProcessingRunId");

            migrationBuilder.RenameIndex(
                name: "ix_ai_execution_logs_correlation_id",
                schema: "ai",
                table: "ai_execution_logs",
                newName: "ix_ai_execution_logs_processing_run_id");

            migrationBuilder.AddColumn<Guid>(
                name: "DocumentClassificationId",
                schema: "ai",
                table: "ai_execution_logs",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE ai.ai_execution_logs
                SET "DocumentClassificationId" = CASE
                    WHEN "Operation" = 'DocumentClassification' THEN "DocumentProcessingRunId"
                    ELSE NULL
                END,
                "DocumentProcessingRunId" = CASE
                    WHEN "Operation" = 'DocumentClassification' THEN NULL
                    ELSE "DocumentProcessingRunId"
                END;
                """);

            migrationBuilder.CreateIndex(
                name: "ix_ai_execution_logs_classification_id",
                schema: "ai",
                table: "ai_execution_logs",
                column: "DocumentClassificationId");
        }
    }
}
