using Velopack;

namespace ArmZavuch;

/// <summary>
/// Точка входа WPF: Velopack-хуки до UI, затем запуск приложения.
/// </summary>
internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        VelopackApp.Build()
            .SetAutoApplyOnStartup(false)
            .Run();

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}
