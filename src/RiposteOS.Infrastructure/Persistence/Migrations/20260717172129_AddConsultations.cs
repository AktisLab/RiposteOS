using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RiposteOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddConsultations : Migration
    {
        private static readonly string[] ConsultationDocumentListIndexColumns =
            ["ConsultationId", "AddedAt", "StoredDocumentId"];
        private static readonly string[] ConsultationListIndexColumns =
            ["ResponseDeadline", "Id"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "consultations");

            migrationBuilder.CreateTable(
                name: "consultations",
                schema: "consultations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    OpportunityId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Buyer = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ResponseDeadline = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    NoticeUrl = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_consultations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_consultations_opportunities_OpportunityId",
                        column: x => x.OpportunityId,
                        principalSchema: "sourcing",
                        principalTable: "opportunities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "consultation_documents",
                schema: "consultations",
                columns: table => new
                {
                    ConsultationId = table.Column<Guid>(type: "uuid", nullable: false),
                    StoredDocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AddedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_consultation_documents", x => new { x.ConsultationId, x.StoredDocumentId });
                    table.ForeignKey(
                        name: "FK_consultation_documents_consultations_ConsultationId",
                        column: x => x.ConsultationId,
                        principalSchema: "consultations",
                        principalTable: "consultations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_consultation_documents_stored_documents_StoredDocumentId",
                        column: x => x.StoredDocumentId,
                        principalSchema: "documents",
                        principalTable: "stored_documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_consultation_documents_consultation_added_at_id",
                schema: "consultations",
                table: "consultation_documents",
                columns: ConsultationDocumentListIndexColumns);

            migrationBuilder.CreateIndex(
                name: "ix_consultation_documents_stored_document_id",
                schema: "consultations",
                table: "consultation_documents",
                column: "StoredDocumentId");

            migrationBuilder.CreateIndex(
                name: "ix_consultations_response_deadline_id",
                schema: "consultations",
                table: "consultations",
                columns: ConsultationListIndexColumns);

            migrationBuilder.CreateIndex(
                name: "ux_consultations_opportunity_id",
                schema: "consultations",
                table: "consultations",
                column: "OpportunityId",
                unique: true,
                filter: "\"OpportunityId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "consultation_documents",
                schema: "consultations");

            migrationBuilder.DropTable(
                name: "consultations",
                schema: "consultations");
        }
    }
}
