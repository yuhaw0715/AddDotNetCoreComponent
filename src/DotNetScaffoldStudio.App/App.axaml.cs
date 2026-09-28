using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using DotNetScaffoldStudio.Application;
using DotNetScaffoldStudio.Domain;
using DotNetScaffoldStudio.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace DotNetScaffoldStudio.App;

public sealed partial class App : Avalonia.Application
{
    private ServiceProvider? _serviceProvider;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override async void OnFrameworkInitializationCompleted()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IWorkspaceService, WorkspaceService>();
        services.AddSingleton<IDemoExecutionService, DemoExecutionService>();
        services.AddSingleton<IFileRevealService, FinderRevealService>();
        services.AddSingleton<IUiTextProvider, AvaloniaUiTextProvider>();
        services.AddSingleton<IAtomicFileReplacer, AtomicFileReplacer>();
        services.AddSingleton<ILocalSettingsStore>(provider =>
            new LocalSettingsStore(fileReplacer: provider.GetRequiredService<IAtomicFileReplacer>()));
        services.AddSingleton<ICommandRunner, ProcessCommandRunner>();
        services.AddSingleton<CommandCoordinator>();
        services.AddSingleton<IGitStatusService>(provider =>
            new GitStatusService(provider.GetRequiredService<ICommandRunner>()));
        services.AddSingleton<IFileSnapshotService, FileSnapshotService>();
        services.AddSingleton<IExecutionHistory, SessionExecutionHistory>();
        services.AddSingleton<INetworkAccessAdapter, UserInitiatedNetworkAccessAdapter>();
        services.AddSingleton<ICommandExecutionWorkflow, CommandExecutionWorkflow>();
        services.AddSingleton<IDotNetEnvironmentDiscovery>(provider =>
            new DotNetEnvironmentDiscoveryService(provider.GetRequiredService<ICommandRunner>()));
        services.AddSingleton<IDependencyDiscovery>(provider =>
            new DependencyDiscoveryService(provider.GetRequiredService<IDotNetEnvironmentDiscovery>()));
        services.AddSingleton<DependencyPlanConfirmationPolicy>();
        services.AddSingleton<IDependencyPlanWorkflow>(provider =>
            new DependencyPlanWorkflow(
                provider.GetRequiredService<ICommandExecutionWorkflow>(),
                provider.GetRequiredService<IDependencyDiscovery>(),
                provider.GetRequiredService<DependencyPlanConfirmationPolicy>(),
                provider.GetRequiredService<INetworkAccessAdapter>()));
        services.AddSingleton<ILocalToolInstallationService>(provider =>
            new LocalToolInstallationService(provider.GetRequiredService<IDependencyDiscovery>()));
        services.AddSingleton<INuGetPackageInstallationService>(provider =>
            new NuGetPackageInstallationService(provider.GetRequiredService<IDependencyDiscovery>()));
        services.AddSingleton<IDependencyGuidanceService, DependencyGuidanceService>();
        services.AddSingleton<ParameterValidator>();
        services.AddSingleton<IDotNetNewCommandFactory, DotNetNewCommandFactory>();
        services.AddSingleton<IAspNetScaffoldingCommandFactory, AspNetScaffoldingCommandFactory>();
        services.AddSingleton<IEfCoreCommandFactory, EfCoreCommandFactory>();
        services.AddSingleton<ExpectedOutputConflictChecker>();
        services.AddSingleton<CommandRiskPolicy>();
        services.AddSingleton<GenerationWorkflow>();
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
            var settingsStore = _serviceProvider.GetRequiredService<ILocalSettingsStore>();
            var loadedSettings = await LoadSettingsAsync(settingsStore);
            var viewModel = _serviceProvider.GetRequiredService<MainWindowViewModel>();
            viewModel.RestorePreferences(loadedSettings.Settings.Preferences);

            var settingsSession = new LocalSettingsSession(loadedSettings.Settings);
            var mainWindow = new MainWindow
            {
                DataContext = viewModel
            };
            mainWindow.ApplyWindowSettings(loadedSettings.Settings.Window);

            var recentWorkspaceCount = settingsSession.RecentWorkspaces.Count;
            await settingsSession.RestoreMostRecentWorkspaceAsync(
                viewModel,
                static path => Directory.Exists(path) || File.Exists(path),
                CancellationToken.None);
            if (settingsSession.RecentWorkspaces.Count != recentWorkspaceCount)
            {
                await settingsStore.SaveAsync(
                    settingsSession.CreateDocument(viewModel.CapturePreferences(), mainWindow.CaptureWindowSettings()),
                    CancellationToken.None);
            }

            var isClosing = false;
            mainWindow.Closing += async (_, eventArgs) =>
            {
                if (isClosing)
                {
                    return;
                }

                eventArgs.Cancel = true;
                isClosing = true;
                try
                {
                    await settingsStore.SaveAsync(
                        settingsSession.Capture(viewModel, mainWindow.CaptureWindowSettings()),
                        CancellationToken.None);
                }
                finally
                {
                    eventArgs.Cancel = false;
                    mainWindow.Close();
                }
            };

            desktop.MainWindow = mainWindow;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static async Task<LocalSettingsLoadResult> LoadSettingsAsync(ILocalSettingsStore settingsStore)
    {
        try
        {
            return await settingsStore.LoadAsync(CancellationToken.None);
        }
        catch
        {
            return new(LocalSettingsDocument.Default, LocalSettingsLoadStatus.Defaults);
        }
    }
}
