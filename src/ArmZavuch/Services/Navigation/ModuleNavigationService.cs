using ArmZavuch.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace ArmZavuch.Services.Navigation;

public sealed class ModuleNavigationService : IModuleNavigationService
{
    private readonly IServiceProvider _services;
    private int? _pendingDirectoriesTab;
    private DirectoriesNavigationContext? _pendingDirectoriesContext;

    public ModuleNavigationService(IServiceProvider services) => _services = services;

    public void GoTo(string moduleKey, int? directoriesTabIndex = null)
    {
        _pendingDirectoriesTab = directoriesTabIndex;
        _pendingDirectoriesContext = directoriesTabIndex is int tab
            ? new DirectoriesNavigationContext { TabIndex = tab }
            : null;
        _services.GetRequiredService<MainViewModel>().NavigateToModule(moduleKey);
    }

    public void GoToDirectories(DirectoriesNavigationContext context)
    {
        _pendingDirectoriesTab = context.TabIndex;
        _pendingDirectoriesContext = context;
        _services.GetRequiredService<MainViewModel>().NavigateToModule("Directories");
    }

    public int? ConsumePendingDirectoriesTab()
    {
        var tab = _pendingDirectoriesTab;
        _pendingDirectoriesTab = null;
        return tab;
    }

    public DirectoriesNavigationContext? ConsumePendingDirectoriesContext()
    {
        var context = _pendingDirectoriesContext;
        _pendingDirectoriesContext = null;
        return context;
    }
}
