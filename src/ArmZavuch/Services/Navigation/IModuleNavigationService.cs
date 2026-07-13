namespace ArmZavuch.Services.Navigation;

/// <summary>Переход между модулями приложения.</summary>
public interface IModuleNavigationService
{
    void GoTo(string moduleKey, int? directoriesTabIndex = null);

    void GoToDirectories(DirectoriesNavigationContext context);

    int? ConsumePendingDirectoriesTab();

    DirectoriesNavigationContext? ConsumePendingDirectoriesContext();
}
