"""Publishing Center — publishing_jobs + marketplace_publications

Revision ID: 004
Revises: 003
Create Date: 2026-06-27
"""
from __future__ import annotations

from alembic import op
import sqlalchemy as sa

revision = "004"
down_revision = "003"
branch_labels = None
depends_on = None


def upgrade() -> None:
    op.create_table(
        "publishing_jobs",
        sa.Column("id", sa.String(36), primary_key=True),
        sa.Column("agent_id", sa.String(36), sa.ForeignKey("published_agents.id"), nullable=False, index=True),
        sa.Column("organization_id", sa.String(36), sa.ForeignKey("organizations.id"), nullable=False, index=True),
        sa.Column("current_step", sa.String(32), nullable=False, server_default="analyze"),
        sa.Column("status", sa.String(32), nullable=False, server_default="draft"),
        sa.Column("analysis_json", sa.Text, nullable=True),
        sa.Column("content_json", sa.Text, nullable=True),
        sa.Column("media_json", sa.Text, nullable=True),
        sa.Column("scores_json", sa.Text, nullable=True),
        sa.Column("settings_json", sa.Text, nullable=False, server_default="{}"),
        sa.Column("target_marketplaces_json", sa.Text, nullable=False, server_default='["giseboutique"]'),
        sa.Column("created_at", sa.DateTime, nullable=False),
        sa.Column("updated_at", sa.DateTime, nullable=False),
    )

    op.create_table(
        "marketplace_publications",
        sa.Column("id", sa.String(36), primary_key=True),
        sa.Column("job_id", sa.String(36), sa.ForeignKey("publishing_jobs.id"), nullable=False, index=True),
        sa.Column("agent_id", sa.String(36), sa.ForeignKey("published_agents.id"), nullable=False, index=True),
        sa.Column("organization_id", sa.String(36), sa.ForeignKey("organizations.id"), nullable=False, index=True),
        sa.Column("marketplace_id", sa.String(64), nullable=False, index=True),
        sa.Column("external_product_id", sa.String(255), nullable=True),
        sa.Column("external_url", sa.Text, nullable=True),
        sa.Column("version", sa.String(32), nullable=False, server_default="1.0.0"),
        sa.Column("status", sa.String(32), nullable=False, server_default="pending_review"),
        sa.Column("error", sa.Text, nullable=True),
        sa.Column("published_at", sa.DateTime, nullable=False),
        sa.Column("last_synced_at", sa.DateTime, nullable=True),
    )


def downgrade() -> None:
    op.drop_table("marketplace_publications")
    op.drop_table("publishing_jobs")
