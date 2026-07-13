using System.Windows;
using ArmZavuch.Data;
using ArmZavuch.Data.Migrations;
using ArmZavuch.Data.Repositories;
using ArmZavuch.Services.Dialog;
using ArmZavuch.Services.Excel;
using ArmZavuch.Services.Export;
using ArmZavuch.Services.Rooms;
using ArmZavuch.Services.Recovery;
using ArmZavuch.Services.Navigation;
using ArmZavuch.Services.Save;
using ArmZavuch.Services.Schedule;
using ArmZavuch.Services.Scoring;
using ArmZavuch.Services.Settings;
using ArmZavuch.Services.Shell;
using ArmZavuch.Services.Catalog;
using ArmZavuch.Services.Data;
using ArmZavuch.Services.Staff;
using ArmZavuch.Services.Text;
using ArmZavuch.Services.Undo;
using ArmZavuch.Services.Update;
using ArmZavuch.Services.Validation;
using ArmZavuch.ViewModels;
using ArmZavuch.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ArmZavuch;

/// <summary>
/// Точка входа: DI-контейнер, миграции БД, проверка черновика восстановления.
/// </summary>
public partial class App : Application
{
    private IHost? _host;

    /// <summary>Единственный экземпляр; передаётся из <see cref="Program"/>.</summary>
    internal SingleInstanceGuard? SingleInstance { get; init; }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        DispatcherUnhandledException += (_, args) =>
        {
            var dialogs = _host?.Services.GetService<IAppDialogService>() ?? new AppDialogService();
            dialogs.ShowError("Ошибка", args.Exception.Message);
            args.Handled = true;
            Shutdown(1);
        };

        _host = Host.CreateDefaultBuilder()
            .ConfigureServices(ConfigureServices)
            .Build();

        await _host.StartAsync();

        var recoveryService = _host.Services.GetRequiredService<IRecoveryService>();
        var recoveryChoice = await recoveryService.CheckOnStartupAsync();
        switch (recoveryChoice)
        {
            case RecoveryChoice.ExitApp:
                Shutdown(0);
                return;
            case RecoveryChoice.RestoreDraft:
                await recoveryService.RestoreDraftAsync();
                break;
            case RecoveryChoice.DeleteDraft:
            case RecoveryChoice.UseSaved:
                await recoveryService.DiscardDraftAsync();
                break;
        }

        var migrationRunner = _host.Services.GetRequiredService<MigrationRunner>();
        await migrationRunner.RunAsync();

        var settings = _host.Services.GetRequiredService<AppSettingsService>();
        await settings.LoadAsync();
        await _host.Services.GetRequiredService<BellTemplateAssignmentService>().LoadAsync();

        try
        {
            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            mainWindow.Show();
            mainWindow.ContentRendered += OnMainWindowContentRendered;

            var tray = _host.Services.GetRequiredService<AppTrayService>();
            SingleInstance?.StartWatching(() => tray.Restore());
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Не удалось запустить приложение:\n\n{ex.Message}",
                AppBranding.ProductName,
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            _host.Services.GetService<AppTrayService>()?.Dispose();
            await _host.StopAsync();
            _host.Dispose();
        }

        base.OnExit(e);
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<SqliteConnectionFactory>();
        services.AddSingleton<MigrationRunner>();

        services.AddSingleton<BuildingRepository>();
        services.AddSingleton<SubjectRepository>();
        services.AddSingleton<SchoolClassRepository>();
        services.AddSingleton<TeacherRepository>();
        services.AddSingleton<TeacherStatusRepository>();
        services.AddSingleton<TeacherUnavailabilityRepository>();
        services.AddSingleton<TeacherAbsenceService>();
        services.AddSingleton<SubstitutionRecordRepository>();
        services.AddSingleton<SubstitutionReportService>();
        services.AddSingleton<TeacherBuildingDayRepository>();
        services.AddSingleton<TeacherAvailabilityService>();
        services.AddSingleton<TeacherClassPreferenceSyncService>();
        services.AddSingleton<TeacherCurriculumSyncService>();
        services.AddSingleton<CurriculumTeacherAssignmentService>();
        services.AddSingleton<TextSuggestionService>();
        services.AddSingleton<SubjectCatalogService>();
        services.AddSingleton<RoomRepository>();
        services.AddSingleton<CurriculumRepository>();
        services.AddSingleton<CurriculumTemplateRepository>();
        services.AddSingleton<CurriculumTemplateApplyService>();
        services.AddSingleton<CurriculumTemplateManageService>();
        services.AddSingleton<BellRepository>();
        services.AddSingleton<WeekTemplateRepository>();
        services.AddSingleton<DayOverrideRepository>();
        services.AddSingleton<SchedulePeriodRepository>();
        services.AddSingleton<CalendarRepository>();
        services.AddSingleton<AppSettingsRepository>();

        services.AddSingleton<BellTemplateAssignmentService>();
        services.AddSingleton<DayScheduleResolver>();
        services.AddSingleton<CalendarCountdownService>();
        services.AddSingleton<PeriodGradeReminderService>();
        services.AddSingleton<SubstitutionScorer>();
        services.AddSingleton<SubstitutionExportService>();
        services.AddSingleton<OverviewScheduleExportService>();
        services.AddSingleton<RoomOccupancyService>();
        services.AddSingleton<ScheduleConflictDetector>();
        services.AddSingleton<BuildingTransitionChecker>();
        services.AddSingleton<ConstructorDragHintService>();
        services.AddSingleton<LoadBalanceChecker>();
        services.AddSingleton<ScheduleComplianceChecker>();
        services.AddSingleton<FopWorkloadService>();
        services.AddSingleton<ExcelTemplateService>();
        services.AddSingleton<ExcelImportService>();
        services.AddSingleton<LegacyScheduleImportService>();
        services.AddSingleton<ExcelExportService>();
        services.AddSingleton<AppSettingsService>();
        services.AddSingleton<IAppDataRevisionService, AppDataRevisionService>();
        services.AddSingleton<IAppDataChangeNotifier, AppDataChangeNotifier>();
        services.AddSingleton<DatabaseClearService>();
        services.AddSingleton<AppDataSelectiveImporter>();
        services.AddSingleton<AppDataTransferService>();

        services.AddSingleton<IAppDialogService, AppDialogService>();
        services.AddSingleton<ISaveStateService, SaveStateService>();
        services.AddSingleton<IRecoveryService, RecoveryService>();
        services.AddSingleton<CrudUndoService>();
        services.AddHostedService<DraftAutoSaveService>();

        services.AddSingleton<GitHubReleaseClient>();
        services.AddSingleton<AppUpdateService>();
        services.AddSingleton<AppUpdateCoordinator>();

        services.AddSingleton<IModuleNavigationService, ModuleNavigationService>();
        services.AddSingleton<AppTrayService>();

        services.AddSingleton<DirectoriesViewModel>();
        services.AddSingleton<DispatcherViewModel>();
        services.AddSingleton<ConstructorViewModel>();
        services.AddSingleton<OverviewViewModel>();
        services.AddSingleton<RoomsViewModel>();
        services.AddSingleton<DirectoriesView>();
        services.AddSingleton<DispatcherView>();
        services.AddSingleton<ConstructorView>();
        services.AddSingleton<OverviewView>();
        services.AddSingleton<RoomsView>();

        services.AddTransient<InstructionsViewModel>();
        services.AddTransient<InstructionsView>();

        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();
    }

    private async void OnMainWindowContentRendered(object? sender, EventArgs e)
    {
        if (sender is Window window)
            window.ContentRendered -= OnMainWindowContentRendered;

        try
        {
            var coordinator = _host?.Services.GetService<AppUpdateCoordinator>();
            if (coordinator is not null)
                await coordinator.CheckOnStartupIfDueAsync();
        }
        catch
        {
            // Фоновая проверка не должна мешать работе.
        }
    }
}
