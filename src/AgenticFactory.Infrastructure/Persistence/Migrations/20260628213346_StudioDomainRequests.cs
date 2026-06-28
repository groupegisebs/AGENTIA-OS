using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgenticFactory.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StudioDomainRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StudioDomainRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedByEmail = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    RequestedByName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    DomainName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Industry = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    UseCase = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudioDomainRequests", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StudioDomainRequests_OrganizationId_Status_CreatedAtUtc",
                table: "StudioDomainRequests",
                columns: new[] { "OrganizationId", "Status", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StudioDomainRequests");
        }
    }
}
