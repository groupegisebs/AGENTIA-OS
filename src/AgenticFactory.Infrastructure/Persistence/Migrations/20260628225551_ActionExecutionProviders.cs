using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgenticFactory.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ActionExecutionProviders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ActionExecutionProviders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Category = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ProviderType = table.Column<int>(type: "integer", nullable: false),
                    IsSystem = table.Column<bool>(type: "boolean", nullable: false),
                    SupportsParameters = table.Column<bool>(type: "boolean", nullable: false),
                    SupportsMonitoring = table.Column<bool>(type: "boolean", nullable: false),
                    SupportsRetry = table.Column<bool>(type: "boolean", nullable: false),
                    SupportsRollback = table.Column<bool>(type: "boolean", nullable: false),
                    SupportsScheduling = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Author = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActionExecutionProviders", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActionExecutionProviders_IsEnabled",
                table: "ActionExecutionProviders",
                column: "IsEnabled");

            migrationBuilder.CreateIndex(
                name: "IX_ActionExecutionProviders_ProviderType",
                table: "ActionExecutionProviders",
                column: "ProviderType",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActionExecutionProviders");
        }
    }
}
