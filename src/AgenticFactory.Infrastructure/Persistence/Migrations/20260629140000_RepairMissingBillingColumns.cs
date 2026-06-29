using AgenticFactory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgenticFactory.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Repare les colonnes manquantes si BillingUsageTracking / AgentPayGatewayProductCode
    /// n'ont pas ete appliquees (migrations orphelines sans Designer).
    /// </summary>
    [DbContext(typeof(AgenticFactoryDbContext))]
    [Migration("20260629140000_RepairMissingBillingColumns")]
    public partial class RepairMissingBillingColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "AgentBlueprints" ADD COLUMN IF NOT EXISTS "CreationCostUsd" numeric NOT NULL DEFAULT 0;
                ALTER TABLE "AgentBlueprints" ADD COLUMN IF NOT EXISTS "CompletionTokens" integer NOT NULL DEFAULT 0;
                ALTER TABLE "AgentBlueprints" ADD COLUMN IF NOT EXISTS "PromptTokens" integer NOT NULL DEFAULT 0;
                ALTER TABLE "AgentDeployments" ADD COLUMN IF NOT EXISTS "DeployFeeUsd" numeric NOT NULL DEFAULT 0;
                ALTER TABLE "SubscriptionPlans" ADD COLUMN IF NOT EXISTS "BlueprintCreationFeeUsd" numeric NOT NULL DEFAULT 0;
                ALTER TABLE "SubscriptionPlans" ADD COLUMN IF NOT EXISTS "DeployFeeUsd" numeric NOT NULL DEFAULT 0;
                ALTER TABLE "Agents" ADD COLUMN IF NOT EXISTS "PayGatewayProductCode" character varying(64) NULL;
                """);

            // Enregistrer les migrations orphelines comme appliquees si absentes de l'historique
            migrationBuilder.Sql("""
                INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
                SELECT '20260628230000_BillingUsageTracking', '10.0.9'
                WHERE NOT EXISTS (
                    SELECT 1 FROM "__EFMigrationsHistory"
                    WHERE "MigrationId" = '20260628230000_BillingUsageTracking');

                INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
                SELECT '20260629120000_AgentPayGatewayProductCode', '10.0.9'
                WHERE NOT EXISTS (
                    SELECT 1 FROM "__EFMigrationsHistory"
                    WHERE "MigrationId" = '20260629120000_AgentPayGatewayProductCode');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Pas de rollback — reparation idempotente en production
        }
    }
}
