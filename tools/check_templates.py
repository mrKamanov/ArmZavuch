import sqlite3
from pathlib import Path

db = Path.home() / "AppData/Local/ArmZavuch/school.db"
c = sqlite3.connect(db)
ver = c.execute("SELECT value FROM meta WHERE key='schema_version'").fetchone()
print("schema_version:", ver)
tables = c.execute(
    "SELECT name FROM sqlite_master WHERE type='table' AND name LIKE 'curriculum%'"
).fetchall()
print("tables:", tables)
if ("curriculum_templates",) in tables:
    print("templates:", c.execute("SELECT id, name FROM curriculum_templates").fetchall())
    print("items:", c.execute("SELECT COUNT(*) FROM curriculum_template_items").fetchone())
