using AgenticFactory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgenticFactory.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(AgenticFactoryDbContext))]
    [Migration("20260629120000_AgentPayGatewayProductCode")]
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
