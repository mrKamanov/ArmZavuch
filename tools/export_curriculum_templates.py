"""Экспорт шаблонов нагрузки по параллелям из school.db."""
import json
import sqlite3
import sys
from pathlib import Path

DB = Path.home() / "AppData/Local/ArmZavuch/school.db"
OUT = Path(__file__).resolve().parents[1] / "src/ArmZavuch/Data/CurriculumTemplates"


def load_items(conn: sqlite3.Connection, class_id: int) -> list[dict]:
    rows = conn.execute(
        """
        SELECT s.name, cu.hours_per_week,
               COALESCE(cu.difficulty_score, s.difficulty_score),
               cu.has_subgroups, cu.week_parity
        FROM curriculum cu
        JOIN subjects s ON s.id = cu.subject_id
        WHERE cu.class_id = ?
        ORDER BY s.name
        """,
        (class_id,),
    ).fetchall()
    return [
        {
            "subjectName": r[0],
            "hoursPerWeek": r[1],
            "difficultyScore": r[2],
            "hasSubgroups": bool(r[3]),
            "weekParity": r[4],
        }
        for r in rows
    ]


def canonical_class_id(conn: sqlite3.Connection, grade: int) -> int | None:
    rows = conn.execute(
        """
        SELECT sc.id, COUNT(cu.id) AS n
        FROM school_classes sc
        LEFT JOIN curriculum cu ON cu.class_id = sc.id
        WHERE sc.grade = ?
        GROUP BY sc.id
        HAVING n > 0
        ORDER BY n DESC, sc.letter
        """,
        (grade,),
    ).fetchall()
    return rows[0][0] if rows else None


def export_grade(conn: sqlite3.Connection, grade: int) -> bool:
    cid = canonical_class_id(conn, grade)
    if cid is None:
        print(f"grade{grade}: no data")
        return False
    items = load_items(conn, cid)
    tpl = {
        "name": f"{grade} класс",
        "gradeFrom": grade,
        "gradeTo": grade,
        "isBuiltIn": True,
        "items": items,
    }
    path = OUT / f"grade{grade}.json"
    path.write_text(json.dumps(tpl, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"grade{grade}: {len(items)} items from class id={cid} -> {path.name}")
    return True


def main() -> None:
    if not DB.exists():
        print(f"DB not found: {DB}", file=sys.stderr)
        sys.exit(1)
    OUT.mkdir(parents=True, exist_ok=True)
    conn = sqlite3.connect(DB)
    for grade in [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11]:
        export_grade(conn, grade)


if __name__ == "__main__":
    main()
