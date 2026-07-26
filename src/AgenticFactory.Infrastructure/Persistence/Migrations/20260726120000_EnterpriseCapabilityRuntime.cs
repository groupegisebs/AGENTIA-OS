using System;
using AgenticFactory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgenticFactory.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
[DbContext(typeof(AgenticFactoryDbContext))]
[Migration("20260726120000_EnterpriseCapabilityRuntime")]
public partial class EnterpriseCapabilityRuntime : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE "Agents" ADD COLUMN IF NOT EXISTS "AvatarEmoji" character varying(16) NOT NULL DEFAULT '🤖';
            ALTER TABLE "Agents" ADD COLUMN IF NOT EXISTS "PersonalityStyle" character varying(40) NOT NULL DEFAULT 'Professionnel';
            ALTER TABLE "Agents" ADD COLUMN IF NOT EXISTS "PersonalityTemperature" double precision NOT NULL DEFAULT 0.2;
            ALTER TABLE "Agents" ADD COLUMN IF NOT EXISTS "PersonalityLanguages" character varying(80) NOT NULL DEFAULT 'fr,en';
            ALTER TABLE "Agents" ADD COLUMN IF NOT EXISTS "KpisJson" text NOT NULL DEFAULT '[]';

            CREATE TABLE IF NOT EXISTS "ActionExecutionLogs" (
                "Id" uuid NOT NULL,
                "OrganizationId" uuid NOT NULL,
                "RunId" uuid NOT NULL,
                "AgentId" uuid NOT NULL,
                "AgentActionId" uuid NULL,
                "ExecutionProviderId" uuid NULL,
                "NodeId" character varying(120) NOT NULL DEFAULT '',
                "NodeType" character varying(120) NOT NULL DEFAULT '',
                "Label" character varying(200) NOT NULL DEFAULT '',
                "ProviderType" character varying(80) NOT NULL DEFAULT 'graph-runtime',
                "Status" character varying(40) NOT NULL DEFAULT '',
                "DurationMs" integer NOT NULL DEFAULT 0,
                "ErrorMessage" text NULL,
                "RetryCount" integer NOT NULL DEFAULT 0,
                "InputJson" text NULL,
                "OutputJson" text NULL,
                "StartedAtUtc" timestamp with time zone NOT NULL,
                "CompletedAtUtc" timestamp with time zone NULL,
                "CreatedAtUtc" timestamp with time zone NOT NULL,
                "UpdatedAtUtc" timestamp with time zone NOT NULL,
                CONSTRAINT "PK_ActionExecutionLogs" PRIMARY KEY ("Id")
            );

            CREATE TABLE IF NOT EXISTS "OrganizationSecrets" (
                "Id" uuid NOT NULL,
                "OrganizationId" uuid NOT NULL,
                "Name" character varying(120) NOT NULL,
                "Provider" character varying(80) NULL,
                "CipherText" text NOT NULL,
                "CreatedAtUtc" timestamp with time zone NOT NULL,
                "UpdatedAtUtc" timestamp with time zone NOT NULL,
                CONSTRAINT "PK_OrganizationSecrets" PRIMARY KEY ("Id")
            );

            CREATE TABLE IF NOT EXISTS "AgentMemoryEntries" (
                "Id" uuid NOT NULL,
                "OrganizationId" uuid NOT NULL,
                "AgentId" uuid NOT NULL,
                "RunId" uuid NULL,
                "Key" character varying(120) NOT NULL,
                "Value" text NOT NULL,
                "IsShortTerm" boolean NOT NULL DEFAULT TRUE,
                "ExpiresAtUtc" timestamp with time zone NULL,
                "CreatedAtUtc" timestamp with time zone NOT NULL,
                "UpdatedAtUtc" timestamp with time zone NOT NULL,
                CONSTRAINT "PK_AgentMemoryEntries" PRIMARY KEY ("Id")
            );

            CREATE TABLE IF NOT EXISTS "KnowledgeBases" (
                "Id" uuid NOT NULL,
                "OrganizationId" uuid NOT NULL,
                "AgentId" uuid NOT NULL,
                "Name" character varying(160) NOT NULL,
                "CreatedAtUtc" timestamp with time zone NOT NULL,
                "UpdatedAtUtc" timestamp with time zone NOT NULL,
                CONSTRAINT "PK_KnowledgeBases" PRIMARY KEY ("Id")
            );

            CREATE TABLE IF NOT EXISTS "KnowledgeDocuments" (
                "Id" uuid NOT NULL,
                "OrganizationId" uuid NOT NULL,
                "KnowledgeBaseId" uuid NOT NULL,
                "Title" character varying(200) NOT NULL,
                "Content" text NOT NULL,
                "ContentType" character varying(40) NOT NULL DEFAULT 'text/plain',
                "CreatedAtUtc" timestamp with time zone NOT NULL,
                "UpdatedAtUtc" timestamp with time zone NOT NULL,
                CONSTRAINT "PK_KnowledgeDocuments" PRIMARY KEY ("Id")
            );

            CREATE TABLE IF NOT EXISTS "MarketplaceListings" (
                "Id" uuid NOT NULL,
                "OrganizationId" uuid NOT NULL,
                "AgentId" uuid NOT NULL,
                "Title" character varying(160) NOT NULL,
                "Description" text NOT NULL,
                "Category" character varying(80) NOT NULL DEFAULT 'General',
                "License" character varying(40) NOT NULL DEFAULT 'Mensuelle',
                "PriceUsd" numeric(18,4) NOT NULL DEFAULT 0,
                "Status" character varying(40) NOT NULL DEFAULT 'Draft',
                "PublishedVersionNumber" integer NOT NULL DEFAULT 0,
                "AuthorDisplayName" character varying(320) NULL,
                "DocumentationUrl" character varying(500) NULL,
                "PayGatewayProductCode" character varying(64) NULL,
                "CreatedAtUtc" timestamp with time zone NOT NULL,
                "UpdatedAtUtc" timestamp with time zone NOT NULL,
                CONSTRAINT "PK_MarketplaceListings" PRIMARY KEY ("Id")
            );

            CREATE TABLE IF NOT EXISTS "AuditLogEntries" (
                "Id" uuid NOT NULL,
                "OrganizationId" uuid NOT NULL,
                "Action" character varying(80) NOT NULL,
                "ActorEmail" character varying(320) NULL,
                "ResourceType" character varying(120) NULL,
                "ResourceId" uuid NULL,
                "DetailsJson" text NOT NULL DEFAULT '{}',
                "CreatedAtUtc" timestamp with time zone NOT NULL,
                "UpdatedAtUtc" timestamp with time zone NOT NULL,
                CONSTRAINT "PK_AuditLogEntries" PRIMARY KEY ("Id")
            );

            CREATE INDEX IF NOT EXISTS "IX_ActionExecutionLogs_Org_Run" ON "ActionExecutionLogs" ("OrganizationId", "RunId", "CreatedAtUtc");
            CREATE INDEX IF NOT EXISTS "IX_ActionExecutionLogs_Org_Agent" ON "ActionExecutionLogs" ("OrganizationId", "AgentId", "CreatedAtUtc");
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_OrganizationSecrets_Org_Name" ON "OrganizationSecrets" ("OrganizationId", "Name");
            CREATE INDEX IF NOT EXISTS "IX_AgentMemoryEntries_Org_Agent" ON "AgentMemoryEntries" ("OrganizationId", "AgentId", "CreatedAtUtc");
            CREATE INDEX IF NOT EXISTS "IX_KnowledgeBases_Org_Agent" ON "KnowledgeBases" ("OrganizationId", "AgentId", "Name");
            CREATE INDEX IF NOT EXISTS "IX_KnowledgeDocuments_Org_Kb" ON "KnowledgeDocuments" ("OrganizationId", "KnowledgeBaseId");
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_MarketplaceListings_Org_Agent" ON "MarketplaceListings" ("OrganizationId", "AgentId");
            CREATE INDEX IF NOT EXISTS "IX_MarketplaceListings_Status" ON "MarketplaceListings" ("Status");
            CREATE INDEX IF NOT EXISTS "IX_AuditLogEntries_Org" ON "AuditLogEntries" ("OrganizationId", "CreatedAtUtc");

            INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
            SELECT '20260726120000_EnterpriseCapabilityRuntime', '10.0.0'
            WHERE NOT EXISTS (
                SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260726120000_EnterpriseCapabilityRuntime'
            );
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TABLE IF EXISTS "AuditLogEntries";
            DROP TABLE IF EXISTS "MarketplaceListings";
            DROP TABLE IF EXISTS "KnowledgeDocuments";
            DROP TABLE IF EXISTS "KnowledgeBases";
            DROP TABLE IF EXISTS "AgentMemoryEntries";
            DROP TABLE IF EXISTS "OrganizationSecrets";
            DROP TABLE IF EXISTS "ActionExecutionLogs";
            ALTER TABLE "Agents" DROP COLUMN IF EXISTS "AvatarEmoji";
            ALTER TABLE "Agents" DROP COLUMN IF EXISTS "PersonalityStyle";
            ALTER TABLE "Agents" DROP COLUMN IF EXISTS "PersonalityTemperature";
            ALTER TABLE "Agents" DROP COLUMN IF EXISTS "PersonalityLanguages";
            ALTER TABLE "Agents" DROP COLUMN IF EXISTS "KpisJson";
            DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260726120000_EnterpriseCapabilityRuntime';
            """);
    }
}
