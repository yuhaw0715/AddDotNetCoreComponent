using Avalonia;

namespace DotNetScaffoldStudio.App;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        MacOsDisplayLinkReadiness.WaitForReady(CancellationToken.None);
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
