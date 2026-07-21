using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace RiposteOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAssistantRagModel : Migration
    {
        private static readonly string[] ConversationIndexColumns = ["ConsultationId", "ArchivedAt", "UpdatedAt", "Id"];
        private static readonly string[] MessageIndexColumns = ["ConversationId", "CreatedAt", "Id"];
        private static readonly string[] VectorCosineOperators = ["vector_cosine_ops"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Capabilities",
                schema: "ai",
                table: "providers",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "Chat");

            migrationBuilder.CreateTable(
                name: "assistant_conversations",
                schema: "consultations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    ConsultationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ArchivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_assistant_conversations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_assistant_conversations_consultations_ConsultationId",
                        column: x => x.ConsultationId,
                        principalSchema: "consultations",
                        principalTable: "consultations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "document_passage_embeddings",
                schema: "ai",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    DocumentPassageId = table.Column<Guid>(type: "uuid", nullable: false),
                    TextHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ProviderName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Model = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Dimension = table.Column<int>(type: "integer", nullable: false),
                    Embedding = table.Column<Vector>(type: "vector(1024)", nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    QueuedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FailedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_passage_embeddings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_document_passage_embeddings_document_passages_DocumentPassa~",
                        column: x => x.DocumentPassageId,
                        principalSchema: "documents",
                        principalTable: "document_passages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "assistant_messages",
                schema: "consultations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Content = table.Column<string>(type: "character varying(16000)", maxLength: 16000, nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ProviderName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Model = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FailedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_assistant_messages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_assistant_messages_assistant_conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalSchema: "consultations",
                        principalTable: "assistant_conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "assistant_message_citations",
                schema: "consultations",
                columns: table => new
                {
                    MessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentPassageId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_assistant_message_citations", x => new { x.MessageId, x.DocumentPassageId });
                    table.ForeignKey(
                        name: "FK_assistant_message_citations_assistant_messages_MessageId",
                        column: x => x.MessageId,
                        principalSchema: "consultations",
                        principalTable: "assistant_messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_assistant_message_citations_document_passages_DocumentPassa~",
                        column: x => x.DocumentPassageId,
                        principalSchema: "documents",
                        principalTable: "document_passages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_assistant_conversations_consultation_active_updated_id",
                schema: "consultations",
                table: "assistant_conversations",
                columns: ConversationIndexColumns);

            migrationBuilder.CreateIndex(
                name: "ix_assistant_message_citations_passage_id",
                schema: "consultations",
                table: "assistant_message_citations",
                column: "DocumentPassageId");

            migrationBuilder.CreateIndex(
                name: "ix_assistant_messages_conversation_created_id",
                schema: "consultations",
                table: "assistant_messages",
                columns: MessageIndexColumns);

            migrationBuilder.CreateIndex(
                name: "ix_document_passage_embeddings_embedding_cosine",
                schema: "ai",
                table: "document_passage_embeddings",
                column: "Embedding")
                .Annotation("Npgsql:IndexMethod", "hnsw")
                .Annotation("Npgsql:IndexOperators", VectorCosineOperators);

            migrationBuilder.CreateIndex(
                name: "ix_document_passage_embeddings_status",
                schema: "ai",
                table: "document_passage_embeddings",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "ux_document_passage_embeddings_passage_id",
                schema: "ai",
                table: "document_passage_embeddings",
                column: "DocumentPassageId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "assistant_message_citations",
                schema: "consultations");

            migrationBuilder.DropTable(
                name: "document_passage_embeddings",
                schema: "ai");

            migrationBuilder.DropTable(
                name: "assistant_messages",
                schema: "consultations");

            migrationBuilder.DropTable(
                name: "assistant_conversations",
                schema: "consultations");

            migrationBuilder.DropColumn(
                name: "Capabilities",
                schema: "ai",
                table: "providers");
        }
    }
}
