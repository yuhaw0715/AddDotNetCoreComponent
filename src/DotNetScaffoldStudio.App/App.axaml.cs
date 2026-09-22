using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using DotNetScaffoldStudio.Application;
using DotNetScaffoldStudio.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace DotNetScaffoldStudio.App;

public sealed partial class App : Avalonia.Application
{
    private ServiceProvider? _serviceProvider;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IWorkspaceService, WorkspaceService>();
        services.AddSingleton<IDemoExecutionService, DemoExecutionService>();
        services.AddSingleton<ICommandRunner, ProcessCommandRunner>();
        services.AddSingleton<CommandCoordinator>();
        services.AddSingleton<IGitStatusService>(provider =>
            new GitStatusService(provider.GetRequiredService<ICommandRunner>()));
        services.AddSingleton<IFileSnapshotService, FileSnapshotService>();
        services.AddSingleton<ICommandExecutionWorkflow, CommandExecutionWorkflow>();
        services.AddSingleton<IDotNetEnvironmentDiscovery>(provider =>
            new DotNetEnvironmentDiscoveryService(provider.GetRequiredService<ICommandRunner>()));
        services.AddSingleton<IDotNetTemplateDiscovery>(provider =>
            new DotNetTemplateDiscoveryService(provider.GetRequiredService<ICommandRunner>()));
        services.AddSingleton<ICustomTemplateHelpDiscovery>(provider =>
            new CustomTemplateHelpDiscoveryService(provider.GetRequiredService<ICommandRunner>()));
        services.AddSingleton<ITargetProjectValidator, TargetProjectValidator>();
        services.AddTransient<ValidatedProjectCommandExecutor>();
        services.AddTransient<MainWindowViewModel>();
        _serviceProvider = services.BuildServiceProvider();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = _serviceProvider.GetRequiredService<MainWindowViewModel>()
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
