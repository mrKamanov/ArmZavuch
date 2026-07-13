using ArmZavuch.Data.Repositories;
using ArmZavuch.Models;

namespace ArmZavuch.Services.Catalog;

/// <summary>
/// Создание, копирование, сохранение и удаление пользовательских шаблонов нагрузки.
/// Вход: репозиторий. Выход: id шаблона или результат операции.
/// </summary>
public sealed class CurriculumTemplateManageService
{
    private readonly CurriculumTemplateRepository _templates;

    public CurriculumTemplateManageService(CurriculumTemplateRepository templates) => _templates = templates;

    public async Task<int> CreateAsync(int grade, IReadOnlyList<CurriculumTemplate> existing)
    {
        ValidateGrade(grade);
        var name = SuggestCreateName(existing, grade);
        return await _templates.CreateUserTemplateAsync(name, grade, []);
    }

    public async Task<int> CopyAsync(int sourceId, IReadOnlyList<CurriculumTemplate> existing)
    {
        var source = existing.FirstOrDefault(t => t.Id == sourceId)
            ?? throw new InvalidOperationException("Шаблон не найден");
        var name = SuggestCopyName(existing, source.Name);
        return await _templates.CopyAsUserTemplateAsync(sourceId, name);
    }

    public async Task SaveAsync(
        int templateId,
        string name,
        int grade,
        IReadOnlyList<CurriculumTemplateItem> items,
        IReadOnlyList<CurriculumTemplate> existing)
    {
        var trimmed = name.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            throw new ArgumentException("Укажите название шаблона");
        ValidateGrade(grade);
        ValidateItems(items);
        if (await _templates.NameExistsAsync(trimmed, templateId))
            throw new InvalidOperationException($"Шаблон «{trimmed}» уже существует");

        await _templates.UpdateUserTemplateAsync(templateId, trimmed, grade, items);
    }

    public Task DeleteAsync(int templateId) => _templates.DeleteUserTemplateAsync(templateId);

    public static int ParseGrade(string? raw)
    {
        if (!int.TryParse(raw, out var grade) || grade < 1 || grade > 11)
            throw new ArgumentException("Параллель — от 1 до 11");
        return grade;
    }

    private static void ValidateGrade(int grade)
    {
        if (grade is < 1 or > 11)
            throw new ArgumentException("Параллель — от 1 до 11");
    }

    private static void ValidateItems(IReadOnlyList<CurriculumTemplateItem> items)
    {
        if (items.Count == 0)
            throw new ArgumentException("Добавьте хотя бы один предмет в шаблон");

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.SubjectName))
                throw new ArgumentException("У каждой строки должен быть предмет");
            if (item.HoursPerWeek <= 0)
                throw new ArgumentException("Часы в неделю должны быть больше 0");
            if (item.DifficultyScore < 0)
                throw new ArgumentException("Балл Сивкова не может быть отрицательным");
            if (!names.Add(item.SubjectName.Trim()))
                throw new ArgumentException($"Предмет «{item.SubjectName}» указан дважды");
        }
    }

    public static string SuggestCreateName(IReadOnlyList<CurriculumTemplate> existing, int grade)
    {
        var taken = existing.Select(t => t.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var baseName = $"{grade} класс (свой)";
        if (!taken.Contains(baseName))
            return baseName;

        for (var i = 2; i < 100; i++)
        {
            var name = $"{grade} класс (свой {i})";
            if (!taken.Contains(name))
                return name;
        }

        return $"{grade} класс ({DateTime.Now:HHmmss})";
    }

    public static string SuggestCopyName(IReadOnlyList<CurriculumTemplate> existing, string sourceName)
    {
        var taken = existing.Select(t => t.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var baseName = $"{sourceName} (копия)";
        if (!taken.Contains(baseName))
            return baseName;

        for (var i = 2; i < 100; i++)
        {
            var name = $"{sourceName} (копия {i})";
            if (!taken.Contains(name))
                return name;
        }

        return $"{sourceName} ({DateTime.Now:HHmmss})";
    }
}
