"""OS foundations tables

Revision ID: 003
Revises: 002
Create Date: 2026-06-27
"""

import sqlalchemy as sa
from alembic import op

revision = "003"
down_revision = "002"
branch_labels = None
depends_on = None


def _table_exists(conn: sa.engine.Connection, table_name: str) -> bool:
    return sa.inspect(conn).has_table(table_name)


def upgrade() -> None:
    conn = op.get_bind()

    if not _table_exists(conn, "agent_runtimes"):
        op.create_table(
            "agent_runtimes",
            sa.Column("agent_id", sa.String(36), sa.ForeignKey("published_agents.id"), primary_key=True),
            sa.Column("organization_id", sa.String(36), nullable=False, index=True),
            sa.Column("lifecycle_state", sa.String(16), nullable=False, server_default="stopped"),
            sa.Column("last_error", sa.Text, nullable=True),
            sa.Column("started_at", sa.DateTime, nullable=True),
            sa.Column("suspended_at", sa.DateTime, nullable=True),
            sa.Column("stopped_at", sa.DateTime, nullable=True),
            sa.Column("created_at", sa.DateTime, nullable=False),
            sa.Column("updated_at", sa.DateTime, nullable=False),
        )

    if not _table_exists(conn, "agent_capability_registries"):
        op.create_table(
            "agent_capability_registries",
            sa.Column("agent_id", sa.String(36), sa.ForeignKey("published_agents.id"), primary_key=True),
            sa.Column("organization_id", sa.String(36), nullable=False, index=True),
            sa.Column("tools_json", sa.Text, nullable=False, server_default="[]"),
            sa.Column("actions_json", sa.Text, nullable=False, server_default="[]"),
            sa.Column("events_json", sa.Text, nullable=False, server_default="[]"),
            sa.Column("created_at", sa.DateTime, nullable=False),
            sa.Column("updated_at", sa.DateTime, nullable=False),
        )

    if not _table_exists(conn, "agent_events"):
        op.create_table(
            "agent_events",
            sa.Column("id", sa.String(36), primary_key=True),
            sa.Column("agent_id", sa.String(36), sa.ForeignKey("published_agents.id"), nullable=False, index=True),
            sa.Column("organization_id", sa.String(36), nullable=False, index=True),
            sa.Column("event_type", sa.String(64), nullable=False),
            sa.Column("payload_json", sa.Text, nullable=False, server_default="{}"),
            sa.Column("metadata_json", sa.Text, nullable=False, server_default="{}"),
            sa.Column("created_at", sa.DateTime, nullable=False, index=True),
        )

    if not _table_exists(conn, "agent_memory_entries"):
        op.create_table(
            "agent_memory_entries",
            sa.Column("id", sa.String(36), primary_key=True),
            sa.Column("agent_id", sa.String(36), sa.ForeignKey("published_agents.id"), nullable=False, index=True),
            sa.Column("organization_id", sa.String(36), nullable=False, index=True),
            sa.Column("namespace", sa.String(64), nullable=False, server_default="default", index=True),
            sa.Column("text", sa.Text, nullable=False),
            sa.Column("metadata_json", sa.Text, nullable=False, server_default="{}"),
            sa.Column("created_at", sa.DateTime, nullable=False, index=True),
        )


def downgrade() -> None:
    op.drop_table("agent_memory_entries")
    op.drop_table("agent_events")
    op.drop_table("agent_capability_registries")
    op.drop_table("agent_runtimes")
