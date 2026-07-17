using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RiposteOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialSourcingSchema : Migration
    {
        private static readonly string[] SourceIdentityColumns = ["Source", "SourceId"];
        private static readonly string[] RevisionHistoryColumns = ["OpportunityId", "CreatedAt"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "identity");

            migrationBuilder.EnsureSchema(
                name: "sourcing");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    UserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: true),
                    SecurityStamp = table.Column<string>(type: "text", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true),
                    PhoneNumber = table.Column<string>(type: "text", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "import_runs",
                schema: "sourcing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    Source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    QueuedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FinishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastHeartbeatAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    CurrentPublicationDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Fetched = table.Column<int>(type: "integer", nullable: false),
                    Created = table.Column<int>(type: "integer", nullable: false),
                    Updated = table.Column<int>(type: "integer", nullable: false),
                    Unchanged = table.Column<int>(type: "integer", nullable: false),
                    Skipped = table.Column<int>(type: "integer", nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_import_runs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "opportunities",
                schema: "sourcing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    Source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SourceId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Title = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Buyer = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    MatchScore = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    PublicationDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ResponseDeadline = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Description = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: true),
                    ProcedureType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ContractNature = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    EstimatedValue = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: true),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    ExecutionDuration = table.Column<string>(type: "text", nullable: true),
                    DocumentUrl = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    NoticeUrl = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    RawPayload = table.Column<string>(type: "jsonb", nullable: false),
                    ContentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ImportedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    CountryCodes = table.Column<string[]>(type: "text[]", nullable: false),
                    CpvCodes = table.Column<string[]>(type: "text[]", nullable: false),
                    DepartmentCodes = table.Column<string[]>(type: "text[]", nullable: false),
                    DescriptorCodes = table.Column<string[]>(type: "text[]", nullable: false),
                    DescriptorLabels = table.Column<string[]>(type: "text[]", nullable: false),
                    MatchReasons = table.Column<string[]>(type: "text[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_opportunities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "sourcing_settings",
                schema: "sourcing",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    PageSize = table.Column<int>(type: "integer", nullable: false),
                    PositiveSignalWeight = table.Column<int>(type: "integer", nullable: false),
                    NegativeSignalPenalty = table.Column<int>(type: "integer", nullable: false),
                    PreferredDepartmentBoost = table.Column<int>(type: "integer", nullable: false),
                    CpvWhitelistBoost = table.Column<int>(type: "integer", nullable: false),
                    CpvWatchBoost = table.Column<int>(type: "integer", nullable: false),
                    CpvExclusionPenalty = table.Column<int>(type: "integer", nullable: false),
                    UrgentDeadlineDays = table.Column<int>(type: "integer", nullable: false),
                    UrgentDeadlinePenalty = table.Column<int>(type: "integer", nullable: false),
                    HighRelevanceThreshold = table.Column<int>(type: "integer", nullable: false),
                    BoampCron = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, defaultValue: "0 * * * *"),
                    TedCron = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, defaultValue: "0 * * * *"),
                    PlaceCron = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, defaultValue: "0 6,18 * * *"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    AllowedCountryCodes = table.Column<string[]>(type: "text[]", nullable: false),
                    CpvExcludedPrefixes = table.Column<string[]>(type: "text[]", nullable: false),
                    CpvWatchPrefixes = table.Column<string[]>(type: "text[]", nullable: false),
                    CpvWhitelistPrefixes = table.Column<string[]>(type: "text[]", nullable: false),
                    ExcludedKeywords = table.Column<string[]>(type: "text[]", nullable: false),
                    Keywords = table.Column<string[]>(type: "text[]", nullable: false),
                    NegativeSignals = table.Column<string[]>(type: "text[]", nullable: false),
                    PositiveSignals = table.Column<string[]>(type: "text[]", nullable: false),
                    PreferredDepartmentCodes = table.Column<string[]>(type: "text[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sourcing_settings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "sourcing_sync_states",
                schema: "sourcing",
                columns: table => new
                {
                    Source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    LastSuccessfulPublicationDate = table.Column<DateOnly>(type: "date", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sourcing_sync_states", x => x.Source);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "identity",
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                schema: "identity",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    ProviderKey = table.Column<string>(type: "text", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "text", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                schema: "identity",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "identity",
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                schema: "identity",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "import_issues",
                schema: "sourcing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    Source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SourceId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ErrorCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RawPayload = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_import_issues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_import_issues_import_runs_RunId",
                        column: x => x.RunId,
                        principalSchema: "sourcing",
                        principalTable: "import_runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "opportunity_publications",
                schema: "sourcing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    OpportunityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SourceId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    NoticeUrl = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    DocumentUrl = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    RawPayload = table.Column<string>(type: "jsonb", nullable: false),
                    ContentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    FirstSeenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_opportunity_publications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_opportunity_publications_opportunities_OpportunityId",
                        column: x => x.OpportunityId,
                        principalSchema: "sourcing",
                        principalTable: "opportunities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "opportunity_revisions",
                schema: "sourcing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    OpportunityId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RawPayload = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_opportunity_revisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_opportunity_revisions_opportunities_OpportunityId",
                        column: x => x.OpportunityId,
                        principalSchema: "sourcing",
                        principalTable: "opportunities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                schema: "identity",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                schema: "identity",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                schema: "identity",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                schema: "identity",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                schema: "identity",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                schema: "identity",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                schema: "identity",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_import_issues_run_id",
                schema: "sourcing",
                table: "import_issues",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "ix_import_runs_active_source",
                schema: "sourcing",
                table: "import_runs",
                column: "Source",
                unique: true,
                filter: "\"Status\" IN ('Queued', 'Running')");

            migrationBuilder.CreateIndex(
                name: "ix_import_runs_queued_at",
                schema: "sourcing",
                table: "import_runs",
                column: "QueuedAt");

            migrationBuilder.CreateIndex(
                name: "ix_opportunities_match_score",
                schema: "sourcing",
                table: "opportunities",
                column: "MatchScore");

            migrationBuilder.CreateIndex(
                name: "ix_opportunities_source_source_id",
                schema: "sourcing",
                table: "opportunities",
                columns: SourceIdentityColumns,
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_opportunities_status",
                schema: "sourcing",
                table: "opportunities",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "ix_opportunity_publications_opportunity_id",
                schema: "sourcing",
                table: "opportunity_publications",
                column: "OpportunityId");

            migrationBuilder.CreateIndex(
                name: "ix_opportunity_publications_source_source_id",
                schema: "sourcing",
                table: "opportunity_publications",
                columns: SourceIdentityColumns,
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_opportunity_revisions_opportunity_created_at",
                schema: "sourcing",
                table: "opportunity_revisions",
                columns: RevisionHistoryColumns);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AspNetRoleClaims",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "import_issues",
                schema: "sourcing");

            migrationBuilder.DropTable(
                name: "opportunity_publications",
                schema: "sourcing");

            migrationBuilder.DropTable(
                name: "opportunity_revisions",
                schema: "sourcing");

            migrationBuilder.DropTable(
                name: "sourcing_settings",
                schema: "sourcing");

            migrationBuilder.DropTable(
                name: "sourcing_sync_states",
                schema: "sourcing");

            migrationBuilder.DropTable(
                name: "AspNetRoles",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "AspNetUsers",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "import_runs",
                schema: "sourcing");

            migrationBuilder.DropTable(
                name: "opportunities",
                schema: "sourcing");
        }
    }
}
