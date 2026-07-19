using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RiposteOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentProcessing : Migration
    {
        private static readonly string[] DocumentPassageOrdinalColumns = ["DocumentProcessingRunId", "Ordinal"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "document_processing_runs",
                schema: "documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    StoredDocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    QueuedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FailedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PageCount = table.Column<int>(type: "integer", nullable: false),
                    PassageCount = table.Column<int>(type: "integer", nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_processing_runs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_document_processing_runs_stored_documents_StoredDocumentId",
                        column: x => x.StoredDocumentId,
                        principalSchema: "documents",
                        principalTable: "stored_documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "document_passages",
                schema: "documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    DocumentProcessingRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    Ordinal = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    PageNumber = table.Column<int>(type: "integer", nullable: true),
                    SectionTitle = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SourceLocation = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_passages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_document_passages_document_processing_runs_DocumentProcessi~",
                        column: x => x.DocumentProcessingRunId,
                        principalSchema: "documents",
                        principalTable: "document_processing_runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_document_passages_run_ordinal",
                schema: "documents",
                table: "document_passages",
                columns: DocumentPassageOrdinalColumns,
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_document_processing_runs_stored_document_id",
                schema: "documents",
                table: "document_processing_runs",
                column: "StoredDocumentId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "document_passages",
                schema: "documents");

            migrationBuilder.DropTable(
                name: "document_processing_runs",
                schema: "documents");
        }
    }
}
