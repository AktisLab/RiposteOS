using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RiposteOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAiConfiguration : Migration
    {
        private static readonly string[] ClassificationDocumentIndexColumns = ["ConsultationId", "StoredDocumentId"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "ai");

            migrationBuilder.CreateTable(
                name: "consultation_document_classifications",
                schema: "ai",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    ConsultationId = table.Column<Guid>(type: "uuid", nullable: false),
                    StoredDocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ProposedKind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Confidence = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    ProviderName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Model = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    QueuedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FailedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    evidence_passage_ids = table.Column<List<Guid>>(type: "uuid[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_consultation_document_classifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_consultation_document_classifications_consultations_Consult~",
                        column: x => x.ConsultationId,
                        principalSchema: "consultations",
                        principalTable: "consultations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_consultation_document_classifications_stored_documents_Stor~",
                        column: x => x.StoredDocumentId,
                        principalSchema: "documents",
                        principalTable: "stored_documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "providers",
                schema: "ai",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Protocol = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    BaseUrl = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Model = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ApiKeyEnvironmentVariableName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_providers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "task_assignments",
                schema: "ai",
                columns: table => new
                {
                    Task = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ProviderId = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_assignments", x => x.Task);
                    table.ForeignKey(
                        name: "FK_task_assignments_providers_ProviderId",
                        column: x => x.ProviderId,
                        principalSchema: "ai",
                        principalTable: "providers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_consultation_document_classifications_StoredDocumentId",
                schema: "ai",
                table: "consultation_document_classifications",
                column: "StoredDocumentId");

            migrationBuilder.CreateIndex(
                name: "ux_ai_document_classifications_consultation_document",
                schema: "ai",
                table: "consultation_document_classifications",
                columns: ClassificationDocumentIndexColumns,
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_ai_providers_name",
                schema: "ai",
                table: "providers",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_task_assignments_ProviderId",
                schema: "ai",
                table: "task_assignments",
                column: "ProviderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "consultation_document_classifications",
                schema: "ai");

            migrationBuilder.DropTable(
                name: "task_assignments",
                schema: "ai");

            migrationBuilder.DropTable(
                name: "providers",
                schema: "ai");
        }
    }
}
