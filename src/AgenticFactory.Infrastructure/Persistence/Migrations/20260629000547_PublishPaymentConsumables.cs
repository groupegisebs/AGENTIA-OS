using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgenticFactory.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PublishPaymentConsumables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PublishCreditPackSize",
                table: "SubscriptionPlans",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "PublishCreditPriceUsd",
                table: "SubscriptionPlans",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "PublishModel",
                table: "SubscriptionPlans",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "RunPackPriceUsd",
                table: "SubscriptionPlans",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "RunPackSize",
                table: "SubscriptionPlans",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Kind",
                table: "SubscriptionCheckouts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Quantity",
                table: "SubscriptionCheckouts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ConsumableRunsBalance",
                table: "OrganizationSubscriptions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PublishCredits",
                table: "OrganizationSubscriptions",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PublishCreditPackSize",
                table: "SubscriptionPlans");

            migrationBuilder.DropColumn(
                name: "PublishCreditPriceUsd",
                table: "SubscriptionPlans");

            migrationBuilder.DropColumn(
                name: "PublishModel",
                table: "SubscriptionPlans");

            migrationBuilder.DropColumn(
                name: "RunPackPriceUsd",
                table: "SubscriptionPlans");

            migrationBuilder.DropColumn(
                name: "RunPackSize",
                table: "SubscriptionPlans");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "SubscriptionCheckouts");

            migrationBuilder.DropColumn(
                name: "Quantity",
                table: "SubscriptionCheckouts");

            migrationBuilder.DropColumn(
                name: "ConsumableRunsBalance",
                table: "OrganizationSubscriptions");

            migrationBuilder.DropColumn(
                name: "PublishCredits",
                table: "OrganizationSubscriptions");
        }
    }
}
