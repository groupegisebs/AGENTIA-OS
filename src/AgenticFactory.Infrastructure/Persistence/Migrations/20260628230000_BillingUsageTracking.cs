using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgenticFactory.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BillingUsageTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CreationCostUsd",
                table: "AgentBlueprints",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "CompletionTokens",
                table: "AgentBlueprints",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PromptTokens",
                table: "AgentBlueprints",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "DeployFeeUsd",
                table: "AgentDeployments",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "BlueprintCreationFeeUsd",
                table: "SubscriptionPlans",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DeployFeeUsd",
                table: "SubscriptionPlans",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreationCostUsd",
                table: "AgentBlueprints");

            migrationBuilder.DropColumn(
                name: "CompletionTokens",
                table: "AgentBlueprints");

            migrationBuilder.DropColumn(
                name: "PromptTokens",
                table: "AgentBlueprints");

            migrationBuilder.DropColumn(
                name: "DeployFeeUsd",
                table: "AgentDeployments");

            migrationBuilder.DropColumn(
                name: "BlueprintCreationFeeUsd",
                table: "SubscriptionPlans");

            migrationBuilder.DropColumn(
                name: "DeployFeeUsd",
                table: "SubscriptionPlans");
        }
    }
}
