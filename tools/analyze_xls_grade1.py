# -*- coding: utf-8 -*-
"""Детальный разбор листов 1 класса из РАСПИСАНИЕ 2025.xls."""
import os
import re
from collections import Counter, defaultdict

import xlrd

BASE = r"c:\golpom\АРМ\DOC"
WEEKDAYS = {"ПОНЕДЕЛЬНИК", "ВТОРНИК", "СРЕДА", "ЧЕТВЕРГ", "ПЯТНИЦА", "СУББОТА"}


def find_xls():
    for fn in os.listdir(BASE):
        if fn.endswith(".xls") and "2025" in fn:
            return os.path.join(BASE, fn)
    return None


def parse_sheet(sh):
    title = str(sh.cell_value(0, 0)).strip()
    m = re.search(r"(\d+)\s*([А-ЯЁA-Z]+)", title)
    cls = (m.group(1) + m.group(2)) if m else sh.name
    senior = sh.nrows > 1 and "кабинет" in str(sh.cell_value(1, 1)).lower()
    subj_col = 2 if senior else 1
    teacher_col = 3 if senior else 2

    current_day = None
    lessons_by_day = defaultdict(list)
    teachers = Counter()

    for r in range(2, sh.nrows):
        c0 = str(sh.cell_value(r, 0)).strip()
        c1 = str(sh.cell_value(r, subj_col)).strip()
        c2 = str(sh.cell_value(r, teacher_col)).strip() if sh.ncols > teacher_col else ""

        if c1.upper() in WEEKDAYS:
            current_day = c1.upper()
            continue

        if not current_day:
            continue

        is_time = bool(re.match(r"\d+[.:]\d+", c0) or re.match(r"\d+\.\d+-\d+", c0))
        if not is_time or not c1:
            continue

        subj = c1
        teacher = c2 if c2 and not re.match(r"^\d", c2) else ""
        lessons_by_day[current_day].append((c0, subj, teacher))
        if teacher and "динам" not in subj.lower():
            teachers[teacher] += 1

    return cls, lessons_by_day, teachers


def check_gaps(lessons):
    """Уроки без дин. паузы — проверка окон по порядку строк."""
    regular = [(i, t, s, te) for i, (t, s, te) in enumerate(lessons) if "динам" not in s.lower()]
    if len(regular) < 2:
        return []
    gaps = []
    for j in range(1, len(regular)):
        if regular[j][0] - regular[j - 1][0] > 1:
            gaps.append((regular[j - 1][2], regular[j][2]))
    return gaps


def main():
    path = find_xls()
    if not path:
        print("xls not found")
        return

    wb = xlrd.open_workbook(path)
    targets = {"1И", "1Л", "1и", "1л"}

    for sn in wb.sheet_names():
        sh = wb.sheet_by_name(sn)
        cls, by_day, teachers = parse_sheet(sh)
        if cls.upper() not in {t.upper() for t in targets} and not any(
            x in sn for x in ("1 и", "1 л", "1И", "1Л")
        ):
            continue

        print(f"\n{'='*60}\nКласс {cls} (лист {sn})\n{'='*60}")
        print("Педагоги (часы в сетке):")
        for t, h in teachers.most_common():
            print(f"  {t}: {h}")

        for day in ["ПОНЕДЕЛЬНИК", "ВТОРНИК", "СРЕДА", "ЧЕТВЕРГ", "ПЯТНИЦА", "СУББОТА"]:
            if day not in by_day:
                continue
            lessons = by_day[day]
            gaps = check_gaps(lessons)
            print(f"\n  {day} ({len(lessons)} строк):")
            for t, s, te in lessons:
                mark = " [ДП]" if "динам" in s.lower() else ""
                print(f"    {t:12} | {s[:35]:35} | {te[:30]}{mark}")
            if gaps:
                print(f"    *** ОКНА: {gaps}")
            else:
                print("    (без окон)")

    # сводка по всем листам
    print(f"\n\n{'='*60}\nСВОДКА ПО ВСЕМ {len(wb.sheet_names())} ЛИСТАМ\n{'='*60}")
    total_teachers = Counter()
    sheets_with_gaps = 0
    for sn in wb.sheet_names():
        sh = wb.sheet_by_name(sn)
        cls, by_day, teachers = parse_sheet(sh)
        total_teachers.update(teachers)
        for day, lessons in by_day.items():
            if check_gaps(lessons):
                sheets_with_gaps += 1
                break

    print(f"Уникальных педагогов в ячейках: {len(total_teachers)}")
    print(f"Листов с окнами у класса: {sheets_with_gaps}")
    print("Топ нагрузки в сетке:")
    for t, h in total_teachers.most_common(15):
        print(f"  {h:3} ч | {t}")


if __name__ == "__main__":
    main()
