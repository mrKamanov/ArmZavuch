using System.Windows;
using ArmZavuch.Models;
using ArmZavuch.Services.Data;
using ArmZavuch.Services.Recovery;
using ArmZavuch.Views.Dialogs;

namespace ArmZavuch.Services.Dialog;

public sealed class AppDialogService : IAppDialogService
{
    public AppDialogResult Show(AppDialogOptions options)
    {
        var owner = ResolveOwner(options.Owner);
        var dialog = new AppDialogWindow(options);

        if (owner is not null)
        {
            dialog.Owner = owner;
            dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }
        else
        {
            dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            dialog.ShowInTaskbar = true;
        }

        dialog.ShowDialog();
        return dialog.Result;
    }

    public bool ConfirmDelete(string label, DeleteEntityKind kind = DeleteEntityKind.Generic)
    {
        var (title, message) = DeleteConfirmationText.ForSingle(kind, label);
        return Show(new AppDialogOptions
        {
            Title = title,
            Message = message,
            Kind = AppDialogKind.Warning,
            ShowCloseButton = true,
            Actions =
            [
                new DialogAction { Text = "Отмена", Result = AppDialogResult.No },
                new DialogAction { Text = "Удалить", Result = AppDialogResult.Yes, Style = DialogActionStyle.Danger, IsDefault = true }
            ]
        }) is AppDialogResult.Yes;
    }

    public bool ConfirmDeleteMany(int count, DeleteEntityKind kind = DeleteEntityKind.Generic)
    {
        var (title, message) = DeleteConfirmationText.ForMany(kind, count);
        return Show(new AppDialogOptions
        {
            Title = title,
            Message = message,
            Kind = AppDialogKind.Warning,
            Actions =
            [
                new DialogAction { Text = "Отмена", Result = AppDialogResult.No },
                new DialogAction { Text = "Удалить", Result = AppDialogResult.Yes, Style = DialogActionStyle.Danger, IsDefault = true }
            ]
        }) is AppDialogResult.Yes;
    }

    public bool ConfirmClearAllData() =>
        Show(new AppDialogOptions
        {
            Title = "Очистить все данные?",
            Message =
                "Будут удалены здания, классы, сотрудники, кабинеты, нагрузка классов, расписание, календарь и замены.\n\n" +
                "Встроенные шаблоны звонков и нагрузки по параллелям останутся. Название школы сохранится. Действие необратимо.",
            Kind = AppDialogKind.Warning,
            ShowCloseButton = true,
            Actions =
            [
                new DialogAction { Text = "Отмена", Result = AppDialogResult.No },
                new DialogAction
                {
                    Text = "Очистить всё",
                    Result = AppDialogResult.Yes,
                    Style = DialogActionStyle.Danger,
                    IsDefault = true
                }
            ]
        }) is AppDialogResult.Yes;

    public bool ConfirmClearDirectorySection(DirectoryClearSection section)
    {
        var (title, message) = DirectoryClearConfirmationText.For(section);
        return Show(new AppDialogOptions
        {
            Title = title,
            Message = message,
            Kind = AppDialogKind.Warning,
            ShowCloseButton = true,
            Actions =
            [
                new DialogAction { Text = "Отмена", Result = AppDialogResult.No },
                new DialogAction
                {
                    Text = "Очистить",
                    Result = AppDialogResult.Yes,
                    Style = DialogActionStyle.Danger,
                    IsDefault = true
                }
            ]
        }) is AppDialogResult.Yes;
    }

    public CurriculumClearMode? AskCurriculumClearMode()
    {
        var result = Show(new AppDialogOptions
        {
            Title = "Очистить «Нагрузка»?",
            Message =
                "Выберите, что удалить:\n\n" +
                "• Вся нагрузка — часы по классам и предметам, назначения педагогов.\n" +
                "• Только педагоги — строки нагрузки останутся, сбросятся привязки «кто ведёт».\n\n" +
                "Расписание и справочники классов/предметов не затрагиваются.",
            Kind = AppDialogKind.Warning,
            ShowCloseButton = true,
            ButtonLayout = DialogButtonLayout.Vertical,
            Actions =
            [
                new DialogAction
                {
                    Text = "Удалить всю нагрузку",
                    Result = AppDialogResult.Yes,
                    Style = DialogActionStyle.Danger,
                    IsDefault = true
                },
                new DialogAction
                {
                    Text = "Только назначения педагогов",
                    Result = AppDialogResult.No,
                    Style = DialogActionStyle.Secondary
                },
                new DialogAction { Text = "Отмена", Result = AppDialogResult.Cancel }
            ]
        });

        return result switch
        {
            AppDialogResult.Yes => CurriculumClearMode.All,
            AppDialogResult.No => CurriculumClearMode.TeacherAssignmentsOnly,
            _ => null
        };
    }

    public bool ConfirmImportAppData(AppDataTransferManifest manifest)
    {
        var school = string.IsNullOrWhiteSpace(manifest.SchoolName) ? "не указана" : manifest.SchoolName;
        var exported = manifest.ExportedAt.ToLocalTime();
        return Show(new AppDialogOptions
        {
            Title = "Загрузить все данные?",
            Message =
                $"Текущие данные на этом компьютере будут полностью заменены содержимым архива.\n\n" +
                $"Школа: {school}\n" +
                $"Выгружено: {exported:dd.MM.yyyy HH:mm}\n\n" +
                "Справочники, расписание, календарь и замены будут перезаписаны.",
            Kind = AppDialogKind.Warning,
            ShowCloseButton = true,
            Actions =
            [
                new DialogAction { Text = "Отмена", Result = AppDialogResult.No },
                new DialogAction
                {
                    Text = "Загрузить",
                    Result = AppDialogResult.Yes,
                    Style = DialogActionStyle.Danger,
                    IsDefault = true
                }
            ]
        }) is AppDialogResult.Yes;
    }

    public bool ConfirmSelectiveImportAppData(
        AppDataTransferManifest manifest,
        IReadOnlyList<AppDataTransferSection> sections,
        AppDataImportMode mode)
    {
        var school = string.IsNullOrWhiteSpace(manifest.SchoolName) ? "не указана" : manifest.SchoolName;
        var exported = manifest.ExportedAt.ToLocalTime();
        var sectionNames = string.Join("\n• ", sections.Select(AppDataSectionCatalog.Title));
        var modeLabel = mode == AppDataImportMode.Merge
            ? "дополнить и обновить существующие записи"
            : "заменить выбранные разделы целиком";

        return Show(new AppDialogOptions
        {
            Title = "Загрузить выбранные разделы?",
            Message =
                $"Из архива будут загружены только отмеченные разделы ({modeLabel}).\n\n" +
                $"Школа: {school}\n" +
                $"Выгружено: {exported:dd.MM.yyyy HH:mm}\n\n" +
                $"Разделы:\n• {sectionNames}",
            Kind = AppDialogKind.Warning,
            ShowCloseButton = true,
            Actions =
            [
                new DialogAction { Text = "Отмена", Result = AppDialogResult.No },
                new DialogAction
                {
                    Text = "Загрузить",
                    Result = AppDialogResult.Yes,
                    Style = DialogActionStyle.Primary,
                    IsDefault = true
                }
            ]
        }) is AppDialogResult.Yes;
    }

    public RecoveryChoice AskRecoveryChoice(DateTime draftTime, DateTime? savedTime)
    {
        var savedLine = savedTime is DateTime saved
            ? $"Последнее «Сохранить»: {saved:dd.MM.yyyy HH:mm}"
            : "Явного сохранения ещё не было — в базе только то, что успело записаться раньше.";

        var result = Show(new AppDialogOptions
        {
            Title = "Найден черновик",
            Message =
                "Программа закрылась до кнопки «Сохранить». Автоматически осталась более новая копия данных.\n\n" +
                $"Черновик: {draftTime:dd.MM.yyyy HH:mm}\n" +
                $"{savedLine}\n\n" +
                "С чем открыть программу?",
            Kind = AppDialogKind.Warning,
            ShowCloseButton = true,
            CloseTooltip = "Закрыть программу",
            ButtonLayout = DialogButtonLayout.Vertical,
            Actions =
            [
                new DialogAction
                {
                    Text = "Подставить черновик",
                    Result = AppDialogResult.Yes,
                    Style = DialogActionStyle.Primary,
                    IsDefault = true
                },
                new DialogAction
                {
                    Text = "Открыть последнее сохранение",
                    Result = AppDialogResult.No,
                    Style = DialogActionStyle.Secondary
                }
            ]
        });

        return result switch
        {
            AppDialogResult.Yes => RecoveryChoice.RestoreDraft,
            AppDialogResult.No => RecoveryChoice.UseSaved,
            _ => RecoveryChoice.ExitApp
        };
    }

    public void ShowInfo(string title, string message) =>
        Show(new AppDialogOptions
        {
            Title = title,
            Message = message,
            Kind = AppDialogKind.Info,
            Actions = [OkAction()]
        });

    public void ShowWarning(string title, string message) =>
        Show(new AppDialogOptions
        {
            Title = title,
            Message = message,
            Kind = AppDialogKind.Warning,
            Actions = [OkAction()]
        });

    public void ShowError(string title, string message) =>
        Show(new AppDialogOptions
        {
            Title = title,
            Message = message,
            Kind = AppDialogKind.Error,
            Actions = [OkAction()]
        });

    public void ShowSuccess(string title, string message) =>
        Show(new AppDialogOptions
        {
            Title = title,
            Message = message,
            Kind = AppDialogKind.Success,
            Actions = [OkAction()]
        });

    public bool ConfirmProceed(string title, string message) =>
        Show(new AppDialogOptions
        {
            Title = title,
            Message = message + "\n\nСохранить всё равно?",
            Kind = AppDialogKind.Warning,
            ShowCloseButton = true,
            Actions =
            [
                new DialogAction { Text = "Отмена", Result = AppDialogResult.No },
                new DialogAction { Text = "Всё равно сохранить", Result = AppDialogResult.Yes, Style = DialogActionStyle.Primary, IsDefault = true }
            ]
        }) is AppDialogResult.Yes;

    public bool ConfirmUpdateDespiteUnsaved(string title, string message) =>
        Show(new AppDialogOptions
        {
            Title = title,
            Message = message + "\n\nОбновить всё равно?",
            Kind = AppDialogKind.Warning,
            ShowCloseButton = true,
            Actions =
            [
                new DialogAction { Text = "Отмена", Result = AppDialogResult.No },
                new DialogAction { Text = "Обновить", Result = AppDialogResult.Yes, Style = DialogActionStyle.Primary, IsDefault = true }
            ]
        }) is AppDialogResult.Yes;

    public UpdatePromptChoice AskUpdateAction(string version, string message, bool canAutoInstall)
    {
        var actions = new List<DialogAction>
        {
            new() { Text = "Позже", Result = AppDialogResult.Cancel, Style = DialogActionStyle.Secondary }
        };

        if (canAutoInstall)
        {
            actions.Add(new DialogAction
            {
                Text = "Установить сейчас",
                Result = AppDialogResult.Yes,
                Style = DialogActionStyle.Primary,
                IsDefault = true
            });
        }
        else
        {
            actions.Add(new DialogAction
            {
                Text = "Открыть страницу загрузки",
                Result = AppDialogResult.Yes,
                Style = DialogActionStyle.Primary,
                IsDefault = true
            });
        }

        var result = Show(new AppDialogOptions
        {
            Title = $"Доступно обновление {version}",
            Message = message,
            Kind = AppDialogKind.Info,
            ShowCloseButton = true,
            ButtonLayout = DialogButtonLayout.Vertical,
            Actions = actions
        });

        return result switch
        {
            AppDialogResult.Yes when canAutoInstall => UpdatePromptChoice.InstallNow,
            AppDialogResult.Yes => UpdatePromptChoice.OpenDownloadPage,
            _ => UpdatePromptChoice.Later
        };
    }

    public string? PromptForText(
        string title,
        string message,
        string defaultText,
        string inputLabel = "Название",
        string confirmButtonText = "Создать")
    {
        var owner = ResolveOwner(null);
        var dialog = new AppDialogWindow(new AppDialogOptions
        {
            Title = title,
            Message = message,
            Kind = AppDialogKind.Question,
            ShowCloseButton = true,
            TextPrompt = new AppDialogTextPrompt
            {
                Label = inputLabel,
                DefaultText = defaultText
            },
            Actions =
            [
                new DialogAction { Text = "Отмена", Result = AppDialogResult.No },
                new DialogAction
                {
                    Text = confirmButtonText,
                    Result = AppDialogResult.Yes,
                    Style = DialogActionStyle.Primary,
                    IsDefault = true
                }
            ]
        });

        if (owner is not null)
        {
            dialog.Owner = owner;
            dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }
        else
        {
            dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            dialog.ShowInTaskbar = true;
        }

        dialog.ShowDialog();
        if (dialog.Result != AppDialogResult.Yes)
            return null;

        var text = dialog.EnteredText?.Trim();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static DialogAction OkAction() =>
        new() { Text = "Понятно", Result = AppDialogResult.Yes, Style = DialogActionStyle.Primary, IsDefault = true };

    private static Window? ResolveOwner(Window? explicitOwner)
    {
        if (explicitOwner is not null)
            return explicitOwner;

        if (Application.Current?.MainWindow is { IsLoaded: true } main)
            return main;

        return Application.Current?.Windows.OfType<Window>()
            .FirstOrDefault(w => w is not AppDialogWindow && w.IsLoaded);
    }
}
