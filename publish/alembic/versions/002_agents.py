"""Agent runtime tables

Revision ID: 002
Revises: 001
Create Date: 2026-06-22
"""

import sqlalchemy as sa
from alembic import op

revision = "002"
down_revision = "001"
branch_labels = None
depends_on = None


def upgrade() -> None:
    op.create_table(
        "published_agents",
        sa.Column("id", sa.String(36), primary_key=True),
        sa.Column("organization_id", sa.String(36), sa.ForeignKey("organizations.id"), nullable=False, index=True),
        sa.Column("deployment_id", sa.String(36), nullable=False, unique=True, index=True),
        sa.Column("blueprint_id", sa.String(36), nullable=False),
        sa.Column("title", sa.String(255), nullable=False),
        sa.Column("description", sa.Text, nullable=False, server_default=""),
        sa.Column("category", sa.String(64), nullable=False, server_default="Général"),
        sa.Column("visibility", sa.String(16), nullable=False, server_default="private"),
        sa.Column("status", sa.String(16), nullable=False, server_default="active"),
        sa.Column("manifest_json", sa.Text, nullable=False),
        sa.Column("created_at", sa.DateTime, nullable=False),
        sa.Column("updated_at", sa.DateTime, nullable=False),
    )

    op.create_table(
        "agent_api_keys",
        sa.Column("id", sa.String(36), primary_key=True),
        sa.Column("agent_id", sa.String(36), sa.ForeignKey("published_agents.id"), nullable=False, index=True),
        sa.Column("label", sa.String(128), nullable=False, server_default=""),
        sa.Column("key_hash", sa.String(255), nullable=False, unique=True, index=True),
        sa.Column("created_at", sa.DateTime, nullable=False),
        sa.Column("revoked_at", sa.DateTime, nullable=True),
    )

    op.create_table(
        "agent_invocations",
        sa.Column("id", sa.String(36), primary_key=True),
        sa.Column("agent_id", sa.String(36), sa.ForeignKey("published_agents.id"), nullable=False, index=True),
        sa.Column("organization_id", sa.String(36), nullable=False, index=True),
        sa.Column("input_chars", sa.Integer, nullable=False, server_default="0"),
        sa.Column("latency_ms", sa.Integer, nullable=False, server_default="0"),
        sa.Column("status", sa.String(16), nullable=False, server_default="success"),
        sa.Column("created_at", sa.DateTime, nullable=False),
    )


def downgrade() -> None:
    op.drop_table("agent_invocations")
    op.drop_table("agent_api_keys")
    op.drop_table("published_agents")
