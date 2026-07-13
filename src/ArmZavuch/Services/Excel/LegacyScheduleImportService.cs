using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ArmZavuch.Data;
using ArmZavuch.Data.Repositories;
using ArmZavuch.Models;
using ArmZavuch.Services.Save;
using ArmZavuch.Services.Text;
using ArmZavuch.Services.Validation;
using ExcelDataReader;

namespace ArmZavuch.Services.Excel;

/// <summary>
/// Импорт справочников и нагрузки из legacy-файла «РАСПИСАНИЕ *.xls»
/// (лист = класс, блоки по дням недели).
/// </summary>
public sealed class LegacyScheduleImportService
{
    private static readonly HashSet<string> Weekdays = new(StringComparer.OrdinalIgnoreCase)
    {
        "ПОНЕДЕЛЬНИК", "ВТОРНИК", "СРЕДА", "ЧЕТВЕРГ", "ПЯТНИЦА", "СУББОТА"
    };

    private static readonly HashSet<string> SkipLessonLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        "динамическая пауза"
    };

    private static readonly Regex ClassTitleRegex = new(
        @"^(?<grade>\d+)\s*(?<letter>[А-ЯЁA-Z])\s*класс",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private readonly BuildingRepository _buildings;
    private readonly SubjectRepository _subjects;
    private readonly SchoolClassRepository _classes;
    private readonly TeacherRepository _teachers;
    private readonly RoomRepository _rooms;
    private readonly CurriculumRepository _curriculum;
    private readonly BellRepository _bells;
    private readonly ISaveStateService _saveState;

    public LegacyScheduleImportService(
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

    public async Task<LegacyScheduleImportResult> ImportAsync(string filePath)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var result = new LegacyScheduleImportResult();
        var sheets = ReadSheetsInternal(filePath);

        if (sheets.Any(s => s.Name.Equals("Здания", StringComparison.OrdinalIgnoreCase)))
        {
            result.Errors.Add($"Похоже на шаблон {AppBranding.ProductName} (.xlsx). Используйте «Импорт из Excel».");
            return result;
        }

        if (sheets.Count == 0)
        {
            result.Errors.Add("В файле нет листов с данными.");
            return result;
        }

        var ctx = new LegacyImportContext();
        try
        {
            foreach (var sheet in sheets)
            {
                if (!TryParseClassTitle(sheet.Name, sheet.Rows, out var meta))
                {
                    result.SkippedSheets.Add(sheet.Name);
                    continue;
                }

                ctx.Classes.Add(meta);
                ctx.Buildings.Register(meta.BuildingName ?? LegacyImportNormalizer.DefaultBuilding);

                var senior = sheet.Rows.Count > 1 && IsSeniorLayout(sheet.Rows[1]);
                if (LegacyBellExtractor.ExtractMondaySchedule(sheet.Rows, senior) is { } bellSchedule)
                    ctx.RegisterBellSchedule(meta.Grade, bellSchedule);

                ParseLessonRows(sheet.Rows, meta, ctx, senior);
            }

            result.TeachersCanonical = ctx.Teachers.AllCanonical.Count();
            await PersistAsync(ctx, result);
            if (result.ImportedCount > 0)
                _saveState.MarkDirty();
        }
        catch (Exception ex)
        {
            result.Errors.Add(ex.Message);
        }

        return result;
    }

    private static List<LegacySheet> ReadSheetsInternal(string filePath)
    {
        using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = ExcelReaderFactory.CreateReader(stream);
        var list = new List<LegacySheet>();
        do
        {
            var rows = new List<string?[]>();
            while (reader.Read())
            {
                var row = new string?[reader.FieldCount];
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    var value = reader.GetValue(i);
                    row[i] = value switch
                    {
                        null => null,
                        DateTime dt => dt.ToString("HH:mm"),
                        double d when d == Math.Floor(d) && d < 10000 => ((int)d).ToString(CultureInfo.InvariantCulture),
                        _ => value.ToString()?.Trim()
                    };
                }
                rows.Add(row);
            }

            if (rows.Count > 0)
                list.Add(new LegacySheet(reader.Name, rows));
        } while (reader.NextResult());

        return list;
    }

    private static bool TryParseClassTitle(string sheetName, List<string?[]> rows, out LegacyClassMeta meta)
    {
        meta = default!;
        var title = rows.Count > 0 ? rows[0].ElementAtOrDefault(0)?.Trim() ?? sheetName : sheetName;
        var match = ClassTitleRegex.Match(title);
        if (!match.Success)
            match = ClassTitleRegex.Match(sheetName);
        if (!match.Success)
            return false;

        var grade = int.Parse(match.Groups["grade"].Value, CultureInfo.InvariantCulture);
        var letter = match.Groups["letter"].Value.ToUpperInvariant();
        string? building = null;
        string? homeroom = null;

        var buildingMatch = Regex.Match(title, @"\(([^)]+)\)");
        if (buildingMatch.Success)
            building = LegacyImportNormalizer.NormalizeBuilding(buildingMatch.Groups[1].Value);

        var afterClass = ClassTitleRegex.Replace(title, "").Trim();
        if (buildingMatch.Success)
            afterClass = afterClass.Replace(buildingMatch.Value, "").Trim();
        afterClass = Regex.Replace(afterClass, @"\(\d+\)", "").Trim();
        if (!string.IsNullOrWhiteSpace(afterClass))
            homeroom = LegacyImportNormalizer.StripPunctuation(afterClass);

        meta = new LegacyClassMeta(grade, letter, building, homeroom, sheetName);
        return true;
    }

    private static void ParseLessonRows(List<string?[]> rows, LegacyClassMeta meta, LegacyImportContext ctx, bool senior)
    {
        if (rows.Count < 2)
            return;

        var classKey = $"{meta.Grade}{meta.Letter}";
        var trackCurriculum = ctx.CurriculumClassKeys.Add(classKey);
        var building = ctx.Buildings.Register(meta.BuildingName ?? LegacyImportNormalizer.DefaultBuilding);

        for (var r = 2; r < rows.Count; r++)
        {
            var row = rows[r];
            if (row.All(string.IsNullOrWhiteSpace))
                continue;

            var subjectCol = senior ? 2 : 1;
            var roomCol = senior ? 1 : 2;
            var teacherCol = senior ? 3 : 2;
            var scoreCol = senior ? 4 : 3;

            var subjectRaw = Cell(row, subjectCol);
            if (IsWeekday(subjectRaw))
                continue;

            var subjectParse = LegacyImportNormalizer.ParseSubjectCell(subjectRaw);
            var subject = subjectParse.Name;
            if (string.IsNullOrWhiteSpace(subject) || SkipLessonLabels.Contains(subject))
                continue;

            var timeOrBreak = Cell(row, 0);
            if (SkipLessonLabels.Contains(timeOrBreak)
                || LegacyBellTimeParser.IsDynamicPauseLabel(timeOrBreak))
                continue;

            ctx.Subjects.TryGetValue(subject, out var prevScore);
            var score = ParseScore(Cell(row, scoreCol));
            if (score > prevScore)
                ctx.Subjects[subject] = score;

            var roomRaw = Cell(row, roomCol);
            var teacherRaw = senior ? Cell(row, teacherCol) : "";
            var roomParse = LegacyImportNormalizer.ParseRoomColumn(roomRaw);

            var teacherTokens = LegacyImportNormalizer.ParseTeacherTokens(teacherRaw).ToList();
            teacherTokens.AddRange(roomParse.Teachers);
            if (!senior && teacherTokens.Count == 0 && LegacyImportNormalizer.LooksLikeTeacher(roomRaw))
                teacherTokens.AddRange(LegacyImportNormalizer.ParseTeacherTokens(roomRaw));
            teacherTokens = teacherTokens.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            foreach (var token in teacherTokens)
                ctx.Teachers.Register(token);

            var rooms = roomParse.Rooms
                .Concat(subjectParse.Rooms)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            foreach (var room in rooms)
                ctx.Rooms.Add((building, room));

            if (!trackCurriculum)
                continue;

            var hasSubgroups = LegacyImportNormalizer.DetectSubgroups(subjectRaw, rooms, teacherTokens)
                               || subjectParse.HasSubgroupInName;
            var key = (classKey, subject);
            if (ctx.Curriculum.TryGetValue(key, out var existing))
                ctx.Curriculum[key] = (existing.Hours + 1, existing.HasSubgroups || hasSubgroups);
            else
                ctx.Curriculum[key] = (1, hasSubgroups);
        }
    }

    private static bool IsSeniorLayout(string?[] headerRow)
    {
        var c1 = headerRow.ElementAtOrDefault(1) ?? "";
        var c2 = headerRow.ElementAtOrDefault(2) ?? "";
        return c1.Contains("кабинет", StringComparison.OrdinalIgnoreCase)
               && c2.Contains("предмет", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsWeekday(string? value) =>
        !string.IsNullOrWhiteSpace(value) && Weekdays.Contains(value.Trim());

    private static double ParseScore(string? raw) =>
        double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : 1.0;

    private static string Cell(string?[] row, int index) => row.ElementAtOrDefault(index)?.Trim() ?? "";

    private async Task PersistAsync(LegacyImportContext ctx, LegacyScheduleImportResult result)
    {
        var buildingIds = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var existing in await _buildings.GetAllAsync())
            buildingIds[existing.Name] = existing.Id;

        foreach (var name in ctx.Buildings.AllCanonical)
        {
            if (buildingIds.Keys.Any(k => LegacyImportNormalizer.GetBuildingMergeKey(k) ==
                                          LegacyImportNormalizer.GetBuildingMergeKey(name)))
                continue;

            if (buildingIds.ContainsKey(name))
                continue;

            var id = await _buildings.InsertAsync(new Building
            {
                Name = name,
                ColorHex = BuildingColors.SuggestNext(await _buildings.GetAllAsync())
            });
            buildingIds[name] = id;
            result.BuildingsAdded++;
            result.ImportedCount++;
        }

        var subjectIds = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var existing in await _subjects.GetAllAsync())
            subjectIds[existing.Name] = existing.Id;

        foreach (var (name, score) in ctx.Subjects)
        {
            if (subjectIds.ContainsKey(name))
                continue;
            var id = await _subjects.InsertAsync(new Subject { Name = name, DifficultyScore = score });
            subjectIds[name] = id;
            result.SubjectsAdded++;
            result.ImportedCount++;
        }

        var classIds = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var existing in await _classes.GetAllAsync())
            classIds[existing.DisplayName] = existing.Id;

        foreach (var cls in ctx.Classes.GroupBy(c => $"{c.Grade}{c.Letter}").Select(g => g.First()))
        {
            var key = $"{cls.Grade}{cls.Letter}";
            if (classIds.ContainsKey(key))
                continue;
            var id = await _classes.InsertAsync(new SchoolClass
            {
                Grade = cls.Grade,
                Letter = cls.Letter,
                Shift = ResolveImportShift(ctx, cls.Grade),
                StudentCount = 25
            });
            classIds[key] = id;
            result.ClassesAdded++;
            result.ImportedCount++;

            if (!string.IsNullOrWhiteSpace(cls.HomeroomTeacher))
            {
                foreach (var token in LegacyImportNormalizer.ParseTeacherTokens(cls.HomeroomTeacher))
                    ctx.Teachers.Register(token);
            }
        }

        var existingTeachers = await _teachers.GetAllAsync();
        foreach (var teacher in existingTeachers)
            ctx.Teachers.Register(teacher.FullName);

        var existingTeacherKeys = existingTeachers
            .Select(t => LegacyImportNormalizer.GetTeacherMergeKey(t.FullName))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var teacherName in ctx.Teachers.AllCanonical.OrderBy(t => t))
        {
            var mergeKey = LegacyImportNormalizer.GetTeacherMergeKey(teacherName);
            if (existingTeacherKeys.Contains(mergeKey))
                continue;
            if (await _teachers.FindIdByNameAsync(teacherName) is not null)
                continue;

            await _teachers.InsertAsync(new Teacher
            {
                FullName = teacherName,
                TeacherType = TeacherTypes.Subject,
                MaxLoadHours = 18
            });
            existingTeacherKeys.Add(mergeKey);
            result.TeachersAdded++;
            result.ImportedCount++;
        }

        ResolveBuildingIds(buildingIds, await _buildings.GetAllAsync());

        var existingRooms = await _rooms.GetAllAsync();
        foreach (var (buildingName, number) in ctx.Rooms)
        {
            if (!TryResolveBuildingId(buildingName, buildingIds, out var buildingId))
                continue;

            if (existingRooms.Any(r => r.BuildingId == buildingId
                                       && r.Number.Equals(number, StringComparison.OrdinalIgnoreCase)))
                continue;

            await _rooms.InsertAsync(new Room
            {
                Number = number,
                BuildingId = buildingId,
                Capacity = 30,
                RoomKind = number.Equals("с/з", StringComparison.OrdinalIgnoreCase) ? "Спортзал" : ""
            });
            existingRooms = existingRooms.Append(new Room { BuildingId = buildingId, Number = number }).ToList();
            result.RoomsAdded++;
            result.ImportedCount++;
        }

        var subjectScores = (await _subjects.GetAllAsync())
            .ToDictionary(s => s.Name, s => s.DifficultyScore, StringComparer.OrdinalIgnoreCase);

        foreach (var ((classKey, subjectName), (hours, hasSubgroups)) in ctx.Curriculum)
        {
            if (!classIds.TryGetValue(classKey, out var classId))
                continue;
            if (!subjectIds.TryGetValue(subjectName, out var subjectId))
                continue;

            var difficulty = subjectScores.TryGetValue(subjectName, out var score)
                ? score
                : OfficialSubjectDifficultyReference.DefaultFallback;
            await _curriculum.UpsertAsync(new CurriculumItem
            {
                ClassId = classId,
                SubjectId = subjectId,
                HoursPerWeek = hours,
                HasSubgroups = hasSubgroups,
                WeekParity = CurriculumWeekParity.EveryWeek,
                SubjectDifficultyScore = difficulty
            });
            result.CurriculumRowsAdded++;
            result.ImportedCount++;
        }

        var existingBells = await _bells.GetAllPeriodsAsync();
        var processedTemplates = new HashSet<int>();
        foreach (var profile in ctx.BellProfiles.Values
                     .GroupBy(p => p.Schedule.TemplateName, StringComparer.OrdinalIgnoreCase)
                     .Select(g => g.OrderByDescending(p => p.Schedule.Entries.Count).First()))
        {
            var templateId = await _bells.EnsureTemplateAsync(
                profile.Schedule.TemplateName, profile.MinGrade, profile.MaxGrade);
            await _bells.UpdateTemplateGradesAsync(templateId, profile.MinGrade, profile.MaxGrade);

            if (!processedTemplates.Add(templateId) && existingBells.Any(b => b.TemplateId == templateId))
                continue;

            foreach (var entry in profile.Schedule.Entries)
            {
                if (BellPeriodExists(existingBells, templateId, profile.Schedule.Shift, entry))
                    continue;

                await _bells.InsertPeriodAsync(new BellPeriod
                {
                    TemplateId = templateId,
                    TemplateName = profile.Schedule.TemplateName,
                    TemplateGradeFrom = profile.MinGrade,
                    TemplateGradeTo = profile.MaxGrade,
                    LessonNumber = entry.LessonNumber,
                    Shift = profile.Schedule.Shift,
                    StartTime = entry.StartTime,
                    EndTime = entry.EndTime,
                    PeriodKind = entry.PeriodKind
                });
                existingBells.Add(new BellPeriod
                {
                    TemplateId = templateId,
                    Shift = profile.Schedule.Shift,
                    LessonNumber = entry.LessonNumber,
                    PeriodKind = entry.PeriodKind
                });
                result.BellPeriodsAdded++;
                result.ImportedCount++;
            }
        }

        await _bells.ConsolidateTemplatesAsync();

        result.ProcessedSheets = ctx.Classes.Count;
    }

    private static void ResolveBuildingIds(Dictionary<string, int> buildingIds, List<Building> allBuildings)
    {
        foreach (var building in allBuildings)
            buildingIds.TryAdd(building.Name, building.Id);
    }

    private static bool TryResolveBuildingId(
        string buildingName,
        Dictionary<string, int> buildingIds,
        out int buildingId)
    {
        if (buildingIds.TryGetValue(buildingName, out buildingId))
            return true;

        var key = LegacyImportNormalizer.GetBuildingMergeKey(buildingName);
        foreach (var (name, id) in buildingIds)
        {
            if (LegacyImportNormalizer.GetBuildingMergeKey(name) == key)
            {
                buildingId = id;
                return true;
            }
        }

        buildingId = 0;
        return false;
    }

    private static int ResolveImportShift(LegacyImportContext ctx, int grade)
    {
        var shift = ctx.ResolveShiftForGrade(grade);
        var draft = new SchoolClass { Grade = grade, Letter = "?", Shift = shift };
        return ClassShiftCompliance.MustStudyFirstShiftOnly(draft) ? 1 : shift;
    }

    private static bool BellPeriodExists(
        IReadOnlyList<BellPeriod> existing,
        int templateId,
        int shift,
        LegacyBellEntry entry) =>
        existing.Any(b =>
            b.TemplateId == templateId
            && b.Shift == shift
            && b.LessonNumber == entry.LessonNumber
            && string.Equals(b.PeriodKind, entry.PeriodKind, StringComparison.OrdinalIgnoreCase));

    private sealed record LegacySheet(string Name, List<string?[]> Rows);

    private sealed record LegacyClassMeta(int Grade, string Letter, string? BuildingName, string? HomeroomTeacher, string SheetName);

    private sealed record LegacyBellProfile(LegacyBellSchedule Schedule, int MinGrade, int MaxGrade);

    private sealed class LegacyImportContext
    {
        public LegacyBuildingRegistry Buildings { get; } = new();
        public List<LegacyClassMeta> Classes { get; } = [];
        public Dictionary<string, double> Subjects { get; } = new(StringComparer.OrdinalIgnoreCase);
        public LegacyTeacherRegistry Teachers { get; } = new();
        public HashSet<(string Building, string Number)> Rooms { get; } = [];
        public HashSet<string> CurriculumClassKeys { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<(string ClassKey, string Subject), (double Hours, bool HasSubgroups)> Curriculum { get; } = [];
        public Dictionary<string, LegacyBellProfile> BellProfiles { get; } = new(StringComparer.Ordinal);

        public void RegisterBellSchedule(int grade, LegacyBellSchedule schedule)
        {
            var templateName = schedule.TemplateName;
            var existingPair = BellProfiles.FirstOrDefault(p =>
                p.Value.Schedule.TemplateName.Equals(templateName, StringComparison.OrdinalIgnoreCase));

            if (existingPair.Key is not null)
            {
                var existing = existingPair.Value;
                var minGrade = Math.Min(existing.MinGrade, grade);
                var maxGrade = Math.Max(existing.MaxGrade, grade);
                BellProfiles[existingPair.Key] = existing with
                {
                    MinGrade = minGrade,
                    MaxGrade = maxGrade
                };

                if (schedule.Entries.Count > existing.Schedule.Entries.Count)
                    BellProfiles[existingPair.Key] = new LegacyBellProfile(schedule, minGrade, maxGrade);

                return;
            }

            if (BellProfiles.TryGetValue(schedule.Signature, out var bySignature))
            {
                BellProfiles[schedule.Signature] = bySignature with
                {
                    MinGrade = Math.Min(bySignature.MinGrade, grade),
                    MaxGrade = Math.Max(bySignature.MaxGrade, grade)
                };
                return;
            }

            BellProfiles[schedule.Signature] = new LegacyBellProfile(schedule, grade, grade);
        }

        public int ResolveShiftForGrade(int grade)
        {
            var match = BellProfiles.Values
                .Where(p => grade >= p.MinGrade && grade <= p.MaxGrade)
                .Select(p => p.Schedule.Shift)
                .Distinct()
                .ToList();

            return match.Count == 1 ? match[0] : 1;
        }
    }
}

public sealed class LegacyScheduleImportResult
{
    public int ImportedCount { get; set; }
    public int ProcessedSheets { get; set; }
    public int BuildingsAdded { get; set; }
    public int ClassesAdded { get; set; }
    public int SubjectsAdded { get; set; }
    public int TeachersAdded { get; set; }
    public int TeachersCanonical { get; set; }
    public int RoomsAdded { get; set; }
    public int BellPeriodsAdded { get; set; }
    public int CurriculumRowsAdded { get; set; }
    public List<string> SkippedSheets { get; } = [];
    public List<string> Errors { get; } = [];

    public string Summary =>
        $"Листов классов: {ProcessedSheets}\n" +
        $"Добавлено — здания: {BuildingsAdded}, классы: {ClassesAdded}, предметы: {SubjectsAdded}, " +
        $"сотрудники: {TeachersAdded} (уникальных ФИО: {TeachersCanonical}), кабинеты: {RoomsAdded}, " +
        $"звонки: {BellPeriodsAdded}, нагрузка: {CurriculumRowsAdded}.\n\n" +
        "Звонки берутся из 1-го столбца (достаточно понедельника): уроки, перемены между ними, дин. паузы.\n" +
        "Нормализация: склеенные ФИО и кабинеты (1323 → 13+23), предметы без «каб …»/«группа», синонимы ДКП и Труд/технология.\n" +
        "Сетку расписания соберите в Конструкторе.";
}
