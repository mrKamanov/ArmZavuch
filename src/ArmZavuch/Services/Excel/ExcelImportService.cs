using ArmZavuch.Data;
using ArmZavuch.Data.Repositories;
using ArmZavuch.Models;
using ArmZavuch.Services.Save;
using ArmZavuch.Services.Validation;
using ClosedXML.Excel;

namespace ArmZavuch.Services.Excel;

/// <summary>Импорт справочников из Excel с отчётом (ТЗ §7.1).</summary>
public sealed class ExcelImportService
{
    private readonly BuildingRepository _buildings;
    private readonly SubjectRepository _subjects;
    private readonly SchoolClassRepository _classes;
    private readonly TeacherRepository _teachers;
    private readonly RoomRepository _rooms;
    private readonly CurriculumRepository _curriculum;
    private readonly BellRepository _bells;
    private readonly ISaveStateService _saveState;

    public ExcelImportService(
        BuildingRepository buildings,
        SubjectRepository subjects,
        SchoolClassRepository classes,
        TeacherRepository teachers,
        RoomRepository rooms,
        CurriculumRepository curriculum,
        BellRepository bells,
        ISaveStateService saveState)
    {
        _buildings = buildings;
        _subjects = subjects;
        _classes = classes;
        _teachers = teachers;
        _rooms = rooms;
        _curriculum = curriculum;
        _bells = bells;
        _saveState = saveState;
    }

    public async Task<ImportResult> ImportAsync(string filePath)
    {
        var result = new ImportResult();
        using var workbook = new XLWorkbook(filePath);

        await ImportBuildingsAsync(workbook, result);
        await ImportSubjectsAsync(workbook, result);
        await ImportClassesAsync(workbook, result);
        await ImportTeachersAsync(workbook, result);
        await ImportRoomsAsync(workbook, result);
        await ImportCurriculumAsync(workbook, result);
        await ImportBellsAsync(workbook, result);

        if (result.ImportedCount > 0)
            _saveState.MarkDirty();

        return result;
    }

    private async Task ImportBuildingsAsync(XLWorkbook wb, ImportResult result)
    {
        if (!wb.Worksheets.TryGetWorksheet("Здания", out var ws))
            return;
        foreach (var row in ws.RowsUsed().Skip(1))
        {
            var name = row.Cell(1).GetString().Trim();
            if (string.IsNullOrEmpty(name))
                continue;
            var color = row.Cell(2).GetString().Trim();
            if (string.IsNullOrEmpty(color))
                color = "#2563EB";
            await _buildings.InsertAsync(new Building { Name = name, ColorHex = color });
            result.ImportedCount++;
        }
    }

    private async Task ImportSubjectsAsync(XLWorkbook wb, ImportResult result)
    {
        if (!wb.Worksheets.TryGetWorksheet("Предметы", out var ws))
            return;
        foreach (var row in ws.RowsUsed().Skip(1))
        {
            var name = row.Cell(1).GetString().Trim();
            if (string.IsNullOrEmpty(name))
                continue;
            var score = row.Cell(2).TryGetValue(out double d) ? d : 1.0;
            await _subjects.InsertAsync(new Subject { Name = name, DifficultyScore = score });
            result.ImportedCount++;
        }
    }

    private async Task ImportClassesAsync(XLWorkbook wb, ImportResult result)
    {
        if (!wb.Worksheets.TryGetWorksheet("Классы", out var ws))
            return;
        var buildings = await _buildings.GetAllAsync();
        foreach (var row in ws.RowsUsed().Skip(1))
        {
            if (!row.Cell(1).TryGetValue(out int grade))
                continue;
            var letter = row.Cell(2).GetString().Trim();
            var shift = row.Cell(3).TryGetValue(out int s) ? s : 1;
            var count = row.Cell(4).TryGetValue(out int c) ? c : 25;
            var isCorrectional = ParseYesNo(row.Cell(5).GetString());
            var buildingName = row.Cell(6).GetString().Trim();
            int? buildingId = null;
            if (!string.IsNullOrEmpty(buildingName))
            {
                buildingId = buildings
                    .FirstOrDefault(b => b.Name.Equals(buildingName, StringComparison.OrdinalIgnoreCase))
                    ?.Id;
            }

            var draft = new SchoolClass
            {
                Grade = grade,
                Letter = letter,
                Shift = shift,
                StudentCount = count,
                IsCorrectional = isCorrectional,
                BuildingId = buildingId
            };
            if (ClassShiftCompliance.ViolatesSecondShiftRule(draft))
                draft.Shift = 1;
            await _classes.InsertAsync(draft);
            result.ImportedCount++;
        }
    }

    private static bool ParseYesNo(string value)
    {
        var v = value.Trim();
        if (string.IsNullOrEmpty(v))
            return false;
        return v.Equals("да", StringComparison.OrdinalIgnoreCase)
            || v.Equals("yes", StringComparison.OrdinalIgnoreCase)
            || v.Equals("1")
            || v.Equals("true", StringComparison.OrdinalIgnoreCase)
            || v.Equals("+");
    }

    private static string? NullIfEmpty(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private async Task ImportTeachersAsync(XLWorkbook wb, ImportResult result)
    {
        if (!wb.Worksheets.TryGetWorksheet("Учителя", out var ws))
            return;
        foreach (var row in ws.RowsUsed().Skip(1))
        {
            var name = row.Cell(1).GetString().Trim();
            if (string.IsNullOrEmpty(name))
                continue;
            var type = TeacherTypes.FromDisplay(row.Cell(2).GetString().Trim());
            var jobTitle = row.Cell(3).GetString().Trim();
            var load = row.Cell(4).TryGetValue(out int l) ? l : 18;
            await _teachers.InsertAsync(new Teacher
            {
                FullName = name,
                TeacherType = type,
                JobTitle = string.IsNullOrEmpty(jobTitle) ? null : jobTitle,
                MaxLoadHours = load,
                PrimarySubject = row.Cell(5).GetString().Trim(),
                Phone = NullIfEmpty(row.Cell(8).GetString()),
                ContactUrl = NullIfEmpty(row.Cell(9).GetString()),
                ContactNote = NullIfEmpty(row.Cell(10).GetString())
            });
            result.ImportedCount++;
        }
    }

    private async Task ImportRoomsAsync(XLWorkbook wb, ImportResult result)
    {
        if (!wb.Worksheets.TryGetWorksheet("Кабинеты", out var ws))
            return;
        var buildingList = await _buildings.GetAllAsync();
        foreach (var row in ws.RowsUsed().Skip(1))
        {
            var number = row.Cell(1).GetString().Trim();
            var buildingName = row.Cell(2).GetString().Trim();
            var building = buildingList.FirstOrDefault(b => b.Name == buildingName);
            if (building is null)
            {
                result.Errors.Add($"Кабинет {number}: неизвестное здание «{buildingName}»");
                continue;
            }
            var capacity = row.Cell(3).TryGetValue(out int c) ? c : 30;
            await _rooms.InsertAsync(new Room
            {
                Number = number,
                BuildingId = building.Id,
                Capacity = capacity,
                RoomKind = row.Cell(4).GetString().Trim()
            });
            result.ImportedCount++;
        }
    }

    private async Task ImportCurriculumAsync(XLWorkbook wb, ImportResult result)
    {
        if (!wb.Worksheets.TryGetWorksheet("Нагрузка", out var ws))
            return;
        var subjects = (await _subjects.GetAllAsync()).ToDictionary(s => s.Id);
        foreach (var row in ws.RowsUsed().Skip(1))
        {
            var className = row.Cell(1).GetString().Trim();
            var subjectName = row.Cell(2).GetString().Trim();
            var classId = await _classes.FindIdByDisplayNameAsync(className);
            var subjectId = await _subjects.FindIdByNameAsync(subjectName);
            if (classId is null || subjectId is null)
            {
                result.Errors.Add($"Нагрузка {className}/{subjectName}: класс или предмет не найден");
                continue;
            }
            var hours = row.Cell(3).TryGetValue(out double h) ? h : 1;
            var subgroups = row.Cell(4).GetString().Trim().Equals("да", StringComparison.OrdinalIgnoreCase);
            var weekParity = row.LastCellUsed().Address.ColumnNumber >= 5
                ? CurriculumWeekParity.FromDisplay(row.Cell(5).GetString().Trim())
                : CurriculumWeekParity.EveryWeek;
            await _curriculum.UpsertAsync(new CurriculumItem
            {
                ClassId = classId.Value,
                SubjectId = subjectId.Value,
                HoursPerWeek = hours,
                HasSubgroups = subgroups,
                WeekParity = weekParity,
                SubjectDifficultyScore = subjects.TryGetValue(subjectId.Value, out var subject)
                    ? subject.DifficultyScore
                    : OfficialSubjectDifficultyReference.DefaultFallback
            });
            result.ImportedCount++;
        }
    }

    private async Task ImportBellsAsync(XLWorkbook wb, ImportResult result)
    {
        if (!wb.Worksheets.TryGetWorksheet("Звонки", out var ws))
            return;
        foreach (var row in ws.RowsUsed().Skip(1))
        {
            var templateName = row.Cell(1).GetString().Trim();
            if (string.IsNullOrEmpty(templateName))
                continue;

            var header = ws.Cell(1, 4).GetString().Trim();
            var isNewFormat = header.Contains("Тип", StringComparison.OrdinalIgnoreCase)
                              || header.Contains("Type", StringComparison.OrdinalIgnoreCase);

            int gradeFrom = 1, gradeTo = 11, lesson, shift = 1;
            string kind = BellPeriodKinds.Lesson;
            string start, end;

            if (isNewFormat)
            {
                gradeFrom = row.Cell(2).TryGetValue(out int gf) ? gf : 1;
                gradeTo = row.Cell(3).TryGetValue(out int gt) ? gt : 11;
                kind = BellPeriodKinds.Parse(row.Cell(4).GetString());
                lesson = row.Cell(5).TryGetValue(out int ln) ? ln : 1;
                start = row.Cell(6).GetString().Trim();
                end = row.Cell(7).GetString().Trim();
                shift = row.Cell(8).TryGetValue(out int sh) ? sh : 1;
            }
            else
            {
                lesson = row.Cell(2).TryGetValue(out int ln) ? ln : 1;
                start = row.Cell(3).GetString().Trim();
                end = row.Cell(4).GetString().Trim();
                shift = row.Cell(5).TryGetValue(out int sh) ? sh : 1;
            }

            var templateId = await _bells.EnsureTemplateAsync(templateName, gradeFrom, gradeTo);
            await _bells.UpdateTemplateGradesAsync(templateId, gradeFrom, gradeTo);
            await _bells.InsertPeriodAsync(new BellPeriod
            {
                TemplateId = templateId,
                TemplateName = templateName,
                TemplateGradeFrom = gradeFrom,
                TemplateGradeTo = gradeTo,
                LessonNumber = lesson,
                Shift = shift,
                StartTime = start,
                EndTime = end,
                PeriodKind = kind
            });
            result.ImportedCount++;
        }
    }
}

public sealed class ImportResult
{
    public int ImportedCount { get; set; }
    public List<string> Errors { get; } = [];
}
