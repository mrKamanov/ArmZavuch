using ArmZavuch.Services.Shell;
using Velopack;

namespace ArmZavuch;

/// <summary>
/// Точка входа WPF: Velopack-хуки, один экземпляр, затем запуск UI.
/// </summary>
internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        VelopackApp.Build()
            .SetAutoApplyOnStartup(false)
            .Run();

        using var instance = SingleInstanceGuard.TryAcquire();
        if (instance is null)
            return;

        var app = new App { SingleInstance = instance };
        app.InitializeComponent();
        app.Run();
    }
}
