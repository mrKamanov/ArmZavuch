"""Проверка подгрузки шаблонов (очищает и заново сидит)."""
import sqlite3
from pathlib import Path

db = Path.home() / "AppData/Local/ArmZavuch/school.db"
c = sqlite3.connect(db)
c.execute("DELETE FROM curriculum_template_items")
c.execute("DELETE FROM curriculum_templates")
c.commit()
print("cleared templates")

# simulate app: run Ensure via importing - user will restart app instead
print("Restart app to seed templates")
