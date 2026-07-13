using ArmZavuch.Data;
using ArmZavuch.Data.Repositories;
using ArmZavuch.Models;

namespace ArmZavuch.Services.Catalog;

/// <summary>Эталон ФОП: импорт типовой нагрузки в справочник школы.</summary>
public sealed class FopWorkloadService
{
    private const double HoursTolerance = 0.01;

    private readonly SubjectRepository _subjects;
    private readonly CurriculumRepository _curriculum;
    private readonly SchoolClassRepository _classes;

    public FopWorkloadService(
        SubjectRepository subjects, CurriculumRepository curriculum, SchoolClassRepository classes)
    {
        _subjects = subjects;
        _curriculum = curriculum;
        _classes = classes;
    }

    public IReadOnlyList<FopHoursEntry> GetReferenceForGrade(int grade) =>
        FopWorkloadReference.ForGrade(grade).Where(e => e.HoursPerWeek > 0).ToList();

    public async Task<FopWorkloadImportPreview> PreviewForClassAsync(int classId)
    {
        var cls = await FindClassAsync(classId);
        if (cls is null)
            return new FopWorkloadImportPreview(0, 0, 0);

        var existing = await LoadEveryWeekBySubjectNameAsync(classId);
        var missing = 0;
        var different = 0;
        var matching = 0;

        foreach (var fop in GetReferenceForGrade(cls.Grade))
        {
            if (!existing.TryGetValue(fop.SubjectName, out var row))
            {
                missing++;
                continue;
            }

            if (Math.Abs(row.HoursPerWeek - fop.HoursPerWeek) > HoursTolerance)
                different++;
            else
                matching++;
        }

        return new FopWorkloadImportPreview(missing, different, matching);
    }

    public async Task<FopWorkloadImportResult> ImportForClassAsync(
        int classId,
        FopWorkloadImportOptions options)
    {
        var cls = await FindClassAsync(classId);
        if (cls is null)
            return new FopWorkloadImportResult(0, 0, 0);

        var existing = await LoadEveryWeekBySubjectIdAsync(classId);
        var added = 0;
        var hoursUpdated = 0;
        var skipped = 0;

        foreach (var fop in GetReferenceForGrade(cls.Grade))
        {
            var subjectId = await EnsureSubjectAsync(fop.SubjectName, cls.Grade);
            if (existing.TryGetValue(subjectId, out var row))
            {
                if (options.OverwriteExistingHours
                    && Math.Abs(row.HoursPerWeek - fop.HoursPerWeek) > HoursTolerance)
                {
                    await _curriculum.UpdateHoursAsync(row.Id, fop.HoursPerWeek);
                    hoursUpdated++;
                }
                else
                    skipped++;

                continue;
            }

            await _curriculum.InsertAsync(new CurriculumItem
            {
                ClassId = classId,
                SubjectId = subjectId,
                HoursPerWeek = fop.HoursPerWeek,
                HasSubgroups = false,
                SubjectDifficultyScore = ResolveSivkovScore(fop.SubjectName, cls.Grade)
            });
            existing[subjectId] = new CurriculumItem { Id = -1, SubjectId = subjectId };
            added++;
        }

        return new FopWorkloadImportResult(added, hoursUpdated, skipped);
    }

    public async Task<FopWorkloadImportResult> ImportForGradeAsync(
        int grade,
        FopWorkloadImportOptions options)
    {
        var classes = (await _classes.GetAllAsync()).Where(c => c.Grade == grade).ToList();
        var total = new FopWorkloadImportResult(0, 0, 0);
        foreach (var cls in classes)
        {
            var part = await ImportForClassAsync(cls.Id, options);
            total = new FopWorkloadImportResult(
                total.Added + part.Added,
                total.HoursUpdated + part.HoursUpdated,
                total.Skipped + part.Skipped);
        }

        return total;
    }

    private async Task<SchoolClass?> FindClassAsync(int classId) =>
        (await _classes.GetAllAsync()).FirstOrDefault(c => c.Id == classId);

    private async Task<Dictionary<string, CurriculumItem>> LoadEveryWeekBySubjectNameAsync(int classId)
    {
        var map = new Dictionary<string, CurriculumItem>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in await LoadEveryWeekItemsAsync(classId))
            map[item.SubjectName] = item;
        return map;
    }

    private async Task<Dictionary<int, CurriculumItem>> LoadEveryWeekBySubjectIdAsync(int classId)
    {
        var map = new Dictionary<int, CurriculumItem>();
        foreach (var item in await LoadEveryWeekItemsAsync(classId))
            map[item.SubjectId] = item;
        return map;
    }

    private async Task<List<CurriculumItem>> LoadEveryWeekItemsAsync(int classId) =>
        (await _curriculum.GetAllAsync())
            .Where(c => c.ClassId == classId && c.WeekParity == CurriculumWeekParity.EveryWeek)
            .ToList();

    private async Task<int> EnsureSubjectAsync(string subjectName, int grade)
    {
        if (await _subjects.FindIdByNameAsync(subjectName) is int subjectId)
            return subjectId;

        return await _subjects.InsertAsync(new Subject
        {
            Name = subjectName,
            DifficultyScore = ResolveSivkovScore(subjectName, grade)
        });
    }

    private static double ResolveSivkovScore(string subjectName, int grade) =>
        OfficialSubjectDifficultyReference.ResolveForClass(
            subjectName,
            grade,
            OfficialSubjectDifficultyReference.DefaultFallback);
}
