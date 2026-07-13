"""Заполнить пустую таблицу шаблонов из JSON (если миграция прошла без seed)."""
import json
import sqlite3
from pathlib import Path

DB = Path.home() / "AppData/Local/ArmZavuch/school.db"
JSON_DIR = Path(__file__).resolve().parents[1] / "src/ArmZavuch/Data/CurriculumTemplates"

c = sqlite3.connect(DB)
if c.execute("SELECT COUNT(*) FROM curriculum_templates").fetchone()[0] > 0:
    print("templates already exist")
    raise SystemExit(0)

order = 0
for file in ["grade1.json", "grade2-4.json"]:
    path = JSON_DIR / file
    dto = json.loads(path.read_text(encoding="utf-8"))
    c.execute(
        "INSERT INTO curriculum_templates (name, grade_from, grade_to, is_builtin, sort_order) VALUES (?,?,?,1,?)",
        (dto["name"], dto["gradeFrom"], dto["gradeTo"], order),
    )
    tid = c.execute("SELECT last_insert_rowid()").fetchone()[0]
    for item in dto["items"]:
        c.execute(
            """INSERT INTO curriculum_template_items
            (template_id, subject_name, hours_per_week, difficulty_score, has_subgroups, week_parity, item_grade_from, item_grade_to)
            VALUES (?,?,?,?,?,?,?,?)""",
            (
                tid,
                item["subjectName"],
                item["hoursPerWeek"],
                item["difficultyScore"],
                1 if item["hasSubgroups"] else 0,
                item["weekParity"],
                item.get("gradeFrom", 0),
                item.get("gradeTo", 0),
            ),
        )
    order += 1
    print("inserted", dto["name"], len(dto["items"]), "items")
c.commit()
print("done")
