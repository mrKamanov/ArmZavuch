using ArmZavuch.Data.Repositories;
using ArmZavuch.Models;

namespace ArmZavuch.Services.Catalog;

/// <summary>
/// Применяет шаблон нагрузки к выбранным классам.
/// Вход: шаблон, id классов. Выход: число строк; создаёт отсутствующие предметы.
/// </summary>
public sealed class CurriculumTemplateApplyService
{
    private readonly CurriculumTemplateRepository _templates;
    private readonly SubjectRepository _subjects;
    private readonly CurriculumRepository _curriculum;
    private readonly SchoolClassRepository _classes;

    public CurriculumTemplateApplyService(
        CurriculumTemplateRepository templates,
        SubjectRepository subjects,
        CurriculumRepository curriculum,
        SchoolClassRepository classes)
    {
        _templates = templates;
        _subjects = subjects;
        _curriculum = curriculum;
        _classes = classes;
    }

    public async Task<CurriculumTemplateApplyResult> ApplyAsync(int templateId, IReadOnlyList<int> classIds)
    {
        var allTemplates = await _templates.GetAllAsync();
        var template = allTemplates.FirstOrDefault(t => t.Id == templateId);
        if (template is null)
            return CurriculumTemplateApplyResult.Fail("Шаблон не найден");
        if (classIds.Count == 0)
            return CurriculumTemplateApplyResult.Fail("Отметьте классы для загрузки нагрузки");

        var classMap = (await _classes.GetAllAsync()).ToDictionary(c => c.Id);
        var rows = 0;
        var subjectsCreated = 0;
        var skippedClasses = new List<string>();

        foreach (var classId in classIds)
        {
            if (!classMap.TryGetValue(classId, out var cls))
                continue;

            if (cls.Grade < template.GradeFrom || cls.Grade > template.GradeTo)
            {
                skippedClasses.Add(cls.DisplayName);
                continue;
            }

            foreach (var item in template.ResolveItemsForGrade(cls.Grade))
            {
                var subjectId = await _subjects.FindIdByNameAsync(item.SubjectName);
                if (subjectId is null)
                {
                    subjectId = await _subjects.InsertAsync(new Subject
                    {
                        Name = item.SubjectName,
                        DifficultyScore = item.DifficultyScore
                    });
                    subjectsCreated++;
                }

                await _curriculum.UpsertAsync(new CurriculumItem
                {
                    ClassId = classId,
                    SubjectId = subjectId.Value,
                    HoursPerWeek = item.HoursPerWeek,
                    HasSubgroups = item.HasSubgroups,
                    WeekParity = item.WeekParity,
                    SubjectDifficultyScore = item.DifficultyScore
                });
                rows++;
            }
        }

        return new CurriculumTemplateApplyResult
        {
            Success = true,
            RowsApplied = rows,
            SubjectsCreated = subjectsCreated,
            SkippedClassNames = skippedClasses
        };
    }
}

public sealed class CurriculumTemplateApplyResult
{
    public bool Success { get; init; }
    public string ErrorMessage { get; init; } = "";
    public int RowsApplied { get; init; }
    public int SubjectsCreated { get; init; }
    public IReadOnlyList<string> SkippedClassNames { get; init; } = [];

    public static CurriculumTemplateApplyResult Fail(string message) =>
        new() { Success = false, ErrorMessage = message };
}
