"""Initial schema

Revision ID: 001
Revises:
Create Date: 2026-06-19
"""

from alembic import op

revision = "001"
down_revision = None
branch_labels = None
depends_on = None


def upgrade() -> None:
    # Tables created via init_db / create_all — revision marker for Alembic workflow
    pass


def downgrade() -> None:
    pass
