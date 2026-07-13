"""Синхронизация встроенных шаблонов и баллов Сивкова в subjects из JSON."""
import json
import sqlite3
from pathlib import Path

DB = Path.home() / "AppData/Local/ArmZavuch/school.db"
JSON_DIR = Path(__file__).resolve().parents[1] / "src/ArmZavuch/Data/CurriculumTemplates"


def load_dto(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8"))


def upsert_template(conn: sqlite3.Connection, dto: dict, sort_order: int) -> None:
    row = conn.execute(
        "SELECT id FROM curriculum_templates WHERE is_builtin=1 AND name=?",
        (dto["name"],),
    ).fetchone()
    if row is None:
        conn.execute(
            "INSERT INTO curriculum_templates (name, grade_from, grade_to, is_builtin, sort_order) VALUES (?,?,?,1,?)",
            (dto["name"], dto["gradeFrom"], dto["gradeTo"], sort_order),
        )
        template_id = conn.execute("SELECT last_insert_rowid()").fetchone()[0]
        print("added", dto["name"])
    else:
        template_id = row[0]
        conn.execute(
            "UPDATE curriculum_templates SET grade_from=?, grade_to=?, sort_order=? WHERE id=?",
            (dto["gradeFrom"], dto["gradeTo"], sort_order, template_id),
        )
        conn.execute("DELETE FROM curriculum_template_items WHERE template_id=?", (template_id,))
        print("updated", dto["name"])

    for item in dto["items"]:
        conn.execute(
            """INSERT INTO curriculum_template_items
            (template_id, subject_name, hours_per_week, difficulty_score, has_subgroups, week_parity, item_grade_from, item_grade_to)
            VALUES (?,?,?,?,?,?,?,?)""",
            (
                template_id,
                item["subjectName"],
                item["hoursPerWeek"],
                item["difficultyScore"],
                1 if item["hasSubgroups"] else 0,
                item["weekParity"],
                item.get("gradeFrom", 0),
                item.get("gradeTo", 0),
            ),
        )


def refresh_subject_defaults(conn: sqlite3.Connection) -> None:
    scores: dict[str, tuple[int, float]] = {}
    for file in sorted(JSON_DIR.glob("grade*.json")):
        dto = load_dto(file)
        grade = dto["gradeFrom"]
        for item in dto["items"]:
            name = item["subjectName"]
            prev = scores.get(name)
            if prev is None or grade > prev[0]:
                scores[name] = (grade, item["difficultyScore"])

    updated = 0
    for name, (_, score) in scores.items():
        cur = conn.execute(
            "SELECT difficulty_score FROM subjects WHERE name=? COLLATE NOCASE",
            (name,),
        ).fetchone()
        if cur is None:
            continue
        if abs(cur[0] - score) > 1e-6:
            conn.execute(
                "UPDATE subjects SET difficulty_score=? WHERE name=? COLLATE NOCASE",
                (score, name),
            )
            updated += 1
    print("subjects difficulty updated:", updated)


def main() -> None:
    if not DB.exists():
        raise SystemExit(f"DB not found: {DB}")

    conn = sqlite3.connect(DB)
    conn.execute(
        "DELETE FROM curriculum_template_items WHERE template_id IN ("
        "SELECT id FROM curriculum_templates WHERE is_builtin=1 AND grade_from=2 AND grade_to=4)"
    )
    conn.execute(
        "DELETE FROM curriculum_templates WHERE is_builtin=1 AND grade_from=2 AND grade_to=4"
    )

    files = sorted(JSON_DIR.glob("grade*.json"))
    for order, file in enumerate(files):
        dto = load_dto(file)
        if not dto.get("items"):
            continue
        upsert_template(conn, dto, order)

    refresh_subject_defaults(conn)
    conn.commit()
    print(
        "templates:",
        conn.execute("SELECT name FROM curriculum_templates ORDER BY sort_order").fetchall(),
    )


if __name__ == "__main__":
    main()
