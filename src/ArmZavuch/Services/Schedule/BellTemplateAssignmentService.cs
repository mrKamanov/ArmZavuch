using ArmZavuch.Data.Repositories;
using ArmZavuch.Models;
using ArmZavuch.Services.Save;

namespace ArmZavuch.Services.Schedule;

/// <summary>Назначение шаблонов звонков: по умолчанию для 1 / 2–11 классов и override на класс.</summary>
public sealed class BellTemplateAssignmentService
{
    public const string DefaultGrade1Key = "bell.default.grade1";
    public const string DefaultGrade1SecondHalfKey = "bell.default.grade1.secondHalf";
    public const string DefaultShift1Key = "bell.default.shift1";
    public const string DefaultShift2Key = "bell.default.shift2";

    private readonly AppSettingsRepository _settings;
    private readonly BellRepository _bells;
    private readonly ISaveStateService _saveState;

    private string _defaultGrade1 = BellTemplateNaming.Grade1;
    private string _defaultGrade1SecondHalf = BellTemplateNaming.Grade1SecondHalf;
    private string _defaultShift1 = BellTemplateNaming.Standard;
    private string _defaultShift2 = BellTemplateNaming.SecondShift;

    public BellTemplateAssignmentService(
        AppSettingsRepository settings,
        BellRepository bells,
        ISaveStateService saveState)
    {
        _settings = settings;
        _bells = bells;
        _saveState = saveState;
    }

    public string DefaultGrade1 => _defaultGrade1;
    public string DefaultGrade1SecondHalf => _defaultGrade1SecondHalf;
    public string DefaultShift1 => _defaultShift1;
    public string DefaultShift2 => _defaultShift2;

    public async Task LoadAsync()
    {
        _defaultGrade1 = await ReadDefaultAsync(DefaultGrade1Key, BellTemplateNaming.Grade1);
        _defaultGrade1SecondHalf = await ReadDefaultAsync(
            DefaultGrade1SecondHalfKey,
            BellTemplateNaming.Grade1SecondHalf);
        _defaultShift1 = await ReadDefaultAsync(DefaultShift1Key, BellTemplateNaming.Standard);
        _defaultShift2 = await ReadDefaultAsync(DefaultShift2Key, BellTemplateNaming.SecondShift);
    }

    public async Task SaveDefaultsAsync(string grade1, string grade1SecondHalf, string shift1, string shift2)
    {
        _defaultGrade1 = Normalize(grade1, BellTemplateNaming.Grade1);
        _defaultGrade1SecondHalf = Normalize(grade1SecondHalf, BellTemplateNaming.Grade1SecondHalf);
        _defaultShift1 = Normalize(shift1, BellTemplateNaming.Standard);
        _defaultShift2 = Normalize(shift2, BellTemplateNaming.SecondShift);

        await _settings.SetAsync(DefaultGrade1Key, _defaultGrade1);
        await _settings.SetAsync(DefaultGrade1SecondHalfKey, _defaultGrade1SecondHalf);
        await _settings.SetAsync(DefaultShift1Key, _defaultShift1);
        await _settings.SetAsync(DefaultShift2Key, _defaultShift2);
        _saveState.MarkDirty();
    }

    public BellTemplateAssignmentSnapshot CreateSnapshot(
        IReadOnlyList<SchoolClass> classes,
        DateOnly? asOfDate = null)
    {
        var templateByClass = new Dictionary<int, string>(classes.Count);
        var custom = new HashSet<int>();

        foreach (var cls in classes)
        {
            templateByClass[cls.Id] = ResolveTemplateName(cls, asOfDate);
            if (cls.BellTemplateId.HasValue)
                custom.Add(cls.Id);
        }

        return new BellTemplateAssignmentSnapshot
        {
            TemplateByClassId = templateByClass,
            CustomClassIds = custom,
            DefaultGrade1 = _defaultGrade1,
            DefaultGrade1SecondHalf = _defaultGrade1SecondHalf,
            DefaultShift1 = _defaultShift1,
            DefaultShift2 = _defaultShift2,
            AsOfDate = asOfDate
        };
    }

    public string ResolveTemplateName(SchoolClass cls, DateOnly? asOfDate = null)
    {
        if (cls.BellTemplateId.HasValue && !string.IsNullOrWhiteSpace(cls.BellTemplateName))
            return cls.BellTemplateName.Trim();

        if (cls.Grade == 1)
        {
            if (asOfDate.HasValue && Grade1BellSemesterRules.UseSecondHalfTemplate(asOfDate.Value))
                return _defaultGrade1SecondHalf;
            return _defaultGrade1;
        }

        return cls.Shift == 2 ? _defaultShift2 : _defaultShift1;
    }

    public async Task<int?> ResolveTemplateIdAsync(string? templateName)
    {
        if (string.IsNullOrWhiteSpace(templateName))
            return null;

        return await _bells.FindTemplateIdByNameAsync(templateName.Trim());
    }

    private async Task<string> ReadDefaultAsync(string key, string fallback)
    {
        var value = await _settings.GetAsync(key);
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private static string Normalize(string value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
}
