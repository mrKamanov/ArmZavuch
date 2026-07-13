using ArmZavuch.Data.Repositories;
using ClosedXML.Excel;
using Microsoft.Win32;

namespace ArmZavuch.Services.Excel;

/// <summary>Выгрузка справочников в Excel (ТЗ §7.2).</summary>
public sealed class ExcelExportService
{
    private readonly BuildingRepository _buildings;
    private readonly SubjectRepository _subjects;
    private readonly SchoolClassRepository _classes;
    private readonly TeacherRepository _teachers;
    private readonly RoomRepository _rooms;
    private readonly CurriculumRepository _curriculum;
    private readonly BellRepository _bells;

    public ExcelExportService(
        BuildingRepository buildings, SubjectRepository subjects, SchoolClassRepository classes,
        TeacherRepository teachers, RoomRepository rooms, CurriculumRepository curriculum,
        BellRepository bells)
    {
        _buildings = buildings;
        _subjects = subjects;
        _classes = classes;
        _teachers = teachers;
        _rooms = rooms;
        _curriculum = curriculum;
        _bells = bells;
    }

    public async Task ExportDirectoriesAsync()
    {
        var dlg = new SaveFileDialog
        {
            Filter = "Excel (*.xlsx)|*.xlsx",
            FileName = $"Расписание_Про_справочники_{DateTime.Today:yyyy-MM-dd}.xlsx"
        };
        if (dlg.ShowDialog() != true)
            return;

        using var wb = new XLWorkbook();

        var buildings = await _buildings.GetAllAsync();
        var bSheet = wb.Worksheets.Add("Здания");
        WriteHeader(bSheet, "Название", "Цвет_HEX");
        var br = 2;
        foreach (var b in buildings)
        {
            bSheet.Cell(br, 1).Value = b.Name;
            bSheet.Cell(br, 2).Value = b.ColorHex;
            br++;
        }

        var routes = await _buildings.GetRoutesAsync();
        var rSheet = wb.Worksheets.Add("Переходы");
        WriteHeader(rSheet, "Здание_от", "Здание_до", "Минуты");
        var rr = 2;
        foreach (var r in routes)
        {
            rSheet.Cell(rr, 1).Value = r.FromBuildingName;
            rSheet.Cell(rr, 2).Value = r.ToBuildingName;
            rSheet.Cell(rr, 3).Value = r.Minutes;
            rr++;
        }

        var rooms = await _rooms.GetAllAsync();
        var rmSheet = wb.Worksheets.Add("Кабинеты");
        WriteHeader(rmSheet, "Номер", "Здание", "Вместимость", "Специфика", "Закреплённый_учитель");
        var rm = 2;
        foreach (var r in rooms)
        {
            rmSheet.Cell(rm, 1).Value = r.Number;
            rmSheet.Cell(rm, 2).Value = r.BuildingName;
            rmSheet.Cell(rm, 3).Value = r.Capacity;
            rmSheet.Cell(rm, 4).Value = r.RoomKind;
            rmSheet.Cell(rm, 5).Value = r.AssignedTeacherName ?? "";
            rm++;
        }

        var subjects = await _subjects.GetAllAsync();
        var sSheet = wb.Worksheets.Add("Предметы");
        WriteHeader(sSheet, "Название", "Балл_Сивкова");
        var sr = 2;
        foreach (var s in subjects)
        {
            sSheet.Cell(sr, 1).Value = s.Name;
            sSheet.Cell(sr, 2).Value = s.DifficultyScore;
            sr++;
        }

        var classes = await _classes.GetAllAsync();
        var cSheet = wb.Worksheets.Add("Классы");
        WriteHeader(cSheet, "Параллель", "Буква", "Смена", "Учеников", "Коррекционный", "Здание");
        var cr = 2;
        foreach (var c in classes)
        {
            cSheet.Cell(cr, 1).Value = c.Grade;
            cSheet.Cell(cr, 2).Value = c.Letter;
            cSheet.Cell(cr, 3).Value = c.Shift;
            cSheet.Cell(cr, 4).Value = c.StudentCount;
            cSheet.Cell(cr, 5).Value = c.IsCorrectional ? "Да" : "";
            cSheet.Cell(cr, 6).Value = c.BuildingName;
            cr++;
        }

        var teachers = await _teachers.GetAllAsync();
        var tSheet = wb.Worksheets.Add("Учителя");
        WriteHeader(tSheet, "ФИО", "Тип", "Должность", "Макс_нагрузка", "Основной_профиль",
            "Смежный_профиль", "Классное_руководство", "Телефон", "Контакт_URL", "Контакт_заметка");
        var tr = 2;
        foreach (var t in teachers)
        {
            tSheet.Cell(tr, 1).Value = t.FullName;
            tSheet.Cell(tr, 2).Value = t.TypeDisplay;
            tSheet.Cell(tr, 3).Value = t.JobTitle ?? "";
            tSheet.Cell(tr, 4).Value = t.MaxLoadHours;
            tSheet.Cell(tr, 5).Value = t.PrimarySubject ?? "";
            tSheet.Cell(tr, 6).Value = t.SecondarySubject ?? "";
            tSheet.Cell(tr, 7).Value = t.HomeroomClass ?? "";
            tSheet.Cell(tr, 8).Value = t.Phone ?? "";
            tSheet.Cell(tr, 9).Value = t.ContactUrl ?? "";
            tSheet.Cell(tr, 10).Value = t.ContactNote ?? "";
            tr++;
        }

        var curriculum = await _curriculum.GetAllAsync();
        var cuSheet = wb.Worksheets.Add("Нагрузка");
        WriteHeader(cuSheet, "Класс", "Предмет", "Часов_в_неделю", "Подгруппы", "Неделя");
        var cur = 2;
        foreach (var c in curriculum)
        {
            cuSheet.Cell(cur, 1).Value = c.ClassName;
            cuSheet.Cell(cur, 2).Value = c.SubjectName;
            cuSheet.Cell(cur, 3).Value = c.HoursPerWeek;
            cuSheet.Cell(cur, 4).Value = c.HasSubgroups ? "да" : "нет";
            cuSheet.Cell(cur, 5).Value = c.WeekParityDisplay;
            cur++;
        }

        var bells = await _bells.GetAllPeriodsAsync();
        var belSheet = wb.Worksheets.Add("Звонки");
        WriteHeader(belSheet, "Шаблон", "Параллель_с", "Параллель_по", "Тип", "Номер_урока", "Начало", "Конец", "Смена");
        var bl = 2;
        foreach (var b in bells)
        {
            belSheet.Cell(bl, 1).Value = b.TemplateName;
            belSheet.Cell(bl, 2).Value = b.TemplateGradeFrom;
            belSheet.Cell(bl, 3).Value = b.TemplateGradeTo;
            belSheet.Cell(bl, 4).Value = b.PeriodKindDisplay;
            belSheet.Cell(bl, 5).Value = b.LessonNumber;
            belSheet.Cell(bl, 6).Value = b.StartTime;
            belSheet.Cell(bl, 7).Value = b.EndTime;
            belSheet.Cell(bl, 8).Value = b.Shift;
            bl++;
        }

        wb.SaveAs(dlg.FileName);
    }

    private static void WriteHeader(IXLWorksheet sheet, params string[] headers)
    {
        for (var i = 0; i < headers.Length; i++)
            sheet.Cell(1, i + 1).Value = headers[i];
        sheet.Row(1).Style.Font.Bold = true;
    }
}
