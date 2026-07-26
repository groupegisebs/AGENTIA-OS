using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgenticFactory.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnterpriseCapabilityRuntime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AvatarEmoji",
                table: "Agents",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "🤖");

            migrationBuilder.AddColumn<string>(
                name: "KpisJson",
                table: "Agents",
                type: "text",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "PersonalityLanguages",
                table: "Agents",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "fr,en");

            migrationBuilder.AddColumn<string>(
                name: "PersonalityStyle",
                table: "Agents",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "Professionnel");

            migrationBuilder.AddColumn<double>(
                name: "PersonalityTemperature",
                table: "Agents",
                type: "double precision",
                nullable: false,
                defaultValue: 0.2);

            migrationBuilder.CreateTable(
                name: "ActionExecutionLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    AgentId = table.Column<Guid>(type: "uuid", nullable: false),
                    AgentActionId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExecutionProviderId = table.Column<Guid>(type: "uuid", nullable: true),
                    NodeId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    NodeType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ProviderType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    DurationMs = table.Column<int>(type: "integer", nullable: false),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    RetryCount = table.Column<int>(type: "integer", nullable: false),
                    InputJson = table.Column<string>(type: "text", nullable: true),
                    OutputJson = table.Column<string>(type: "text", nullable: true),
                    StartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActionExecutionLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActionExecutionLogs_AgentRuns_RunId",
                        column: x => x.RunId,
                        principalTable: "AgentRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ActionExecutionLogs_Agents_AgentId",
                        column: x => x.AgentId,
                        principalTable: "Agents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AgentMemoryEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    AgentId = table.Column<Guid>(type: "uuid", nullable: false),
                    RunId = table.Column<Guid>(type: "uuid", nullable: true),
                    Key = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Value = table.Column<string>(type: "text", nullable: false),
                    IsShortTerm = table.Column<bool>(type: "boolean", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentMemoryEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgentMemoryEntries_Agents_AgentId",
                        column: x => x.AgentId,
                        principalTable: "Agents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AuditLogEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ActorEmail = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    ResourceType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ResourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    DetailsJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "KnowledgeBases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    AgentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KnowledgeBases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KnowledgeBases_Agents_AgentId",
                        column: x => x.AgentId,
                        principalTable: "Agents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MarketplaceListings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    AgentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Category = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    License = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    PriceUsd = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    PublishedVersionNumber = table.Column<int>(type: "integer", nullable: false),
                    AuthorDisplayName = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    DocumentationUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PayGatewayProductCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketplaceListings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MarketplaceListings_Agents_AgentId",
                        column: x => x.AgentId,
                        principalTable: "Agents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrganizationSecrets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Provider = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    CipherText = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationSecrets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "KnowledgeDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    KnowledgeBaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    ContentType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KnowledgeDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KnowledgeDocuments_KnowledgeBases_KnowledgeBaseId",
                        column: x => x.KnowledgeBaseId,
                        principalTable: "KnowledgeBases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActionExecutionLogs_AgentId",
                table: "ActionExecutionLogs",
                column: "AgentId");

            migrationBuilder.CreateIndex(
                name: "IX_ActionExecutionLogs_OrganizationId_AgentId_CreatedAtUtc",
                table: "ActionExecutionLogs",
                columns: new[] { "OrganizationId", "AgentId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ActionExecutionLogs_OrganizationId_RunId_CreatedAtUtc",
                table: "ActionExecutionLogs",
                columns: new[] { "OrganizationId", "RunId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ActionExecutionLogs_RunId",
                table: "ActionExecutionLogs",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentMemoryEntries_AgentId",
                table: "AgentMemoryEntries",
                column: "AgentId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentMemoryEntries_OrganizationId_AgentId_CreatedAtUtc",
                table: "AgentMemoryEntries",
                columns: new[] { "OrganizationId", "AgentId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogEntries_OrganizationId_CreatedAtUtc",
                table: "AuditLogEntries",
                columns: new[] { "OrganizationId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeBases_AgentId",
                table: "KnowledgeBases",
                column: "AgentId");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeBases_OrganizationId_AgentId_Name",
                table: "KnowledgeBases",
                columns: new[] { "OrganizationId", "AgentId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeDocuments_KnowledgeBaseId",
                table: "KnowledgeDocuments",
                column: "KnowledgeBaseId");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeDocuments_OrganizationId_KnowledgeBaseId",
                table: "KnowledgeDocuments",
                columns: new[] { "OrganizationId", "KnowledgeBaseId" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketplaceListings_AgentId",
                table: "MarketplaceListings",
                column: "AgentId");

            migrationBuilder.CreateIndex(
                name: "IX_MarketplaceListings_OrganizationId_AgentId",
                table: "MarketplaceListings",
                columns: new[] { "OrganizationId", "AgentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MarketplaceListings_Status",
                table: "MarketplaceListings",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationSecrets_OrganizationId_Name",
                table: "OrganizationSecrets",
                columns: new[] { "OrganizationId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActionExecutionLogs");

            migrationBuilder.DropTable(
                name: "AgentMemoryEntries");

            migrationBuilder.DropTable(
                name: "AuditLogEntries");

            migrationBuilder.DropTable(
                name: "KnowledgeDocuments");

            migrationBuilder.DropTable(
                name: "MarketplaceListings");

            migrationBuilder.DropTable(
                name: "OrganizationSecrets");

            migrationBuilder.DropTable(
                name: "KnowledgeBases");

            migrationBuilder.DropColumn(
                name: "AvatarEmoji",
                table: "Agents");

            migrationBuilder.DropColumn(
                name: "KpisJson",
                table: "Agents");

            migrationBuilder.DropColumn(
                name: "PersonalityLanguages",
                table: "Agents");

            migrationBuilder.DropColumn(
                name: "PersonalityStyle",
                table: "Agents");

            migrationBuilder.DropColumn(
                name: "PersonalityTemperature",
                table: "Agents");
        }
    }
}
