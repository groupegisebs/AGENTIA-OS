-- Reparation manuelle PostgreSQL (VPS) si l'API ne demarre pas
-- Usage: sudo -u postgres psql -d VOTRE_BASE -f repair-billing-schema.sql

ALTER TABLE "AgentBlueprints" ADD COLUMN IF NOT EXISTS "CreationCostUsd" numeric NOT NULL DEFAULT 0;
ALTER TABLE "AgentBlueprints" ADD COLUMN IF NOT EXISTS "CompletionTokens" integer NOT NULL DEFAULT 0;
ALTER TABLE "AgentBlueprints" ADD COLUMN IF NOT EXISTS "PromptTokens" integer NOT NULL DEFAULT 0;
ALTER TABLE "AgentDeployments" ADD COLUMN IF NOT EXISTS "DeployFeeUsd" numeric NOT NULL DEFAULT 0;
ALTER TABLE "SubscriptionPlans" ADD COLUMN IF NOT EXISTS "BlueprintCreationFeeUsd" numeric NOT NULL DEFAULT 0;
ALTER TABLE "SubscriptionPlans" ADD COLUMN IF NOT EXISTS "DeployFeeUsd" numeric NOT NULL DEFAULT 0;
ALTER TABLE "Agents" ADD COLUMN IF NOT EXISTS "PayGatewayProductCode" character varying(64) NULL;

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
