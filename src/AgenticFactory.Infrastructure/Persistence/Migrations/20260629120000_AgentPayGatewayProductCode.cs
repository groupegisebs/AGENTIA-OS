using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgenticFactory.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AgentPayGatewayProductCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PayGatewayProductCode",
                table: "Agents",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PayGatewayProductCode",
                table: "Agents");
        }
    }
}
