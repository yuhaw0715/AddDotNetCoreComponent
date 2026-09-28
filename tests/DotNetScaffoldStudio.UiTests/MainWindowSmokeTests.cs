using DotNetScaffoldStudio.App;
using DotNetScaffoldStudio.Application;
using DotNetScaffoldStudio.Domain;
using DotNetScaffoldStudio.Infrastructure;

namespace DotNetScaffoldStudio.UiTests;

public sealed class MainWindowSmokeTests
{
    [Fact]
    public void Startup_LoadsApplicationResourcesAndMainWindowWithoutDisplayServer()
    {
        var app = new global::DotNetScaffoldStudio.App.App();
        app.Initialize();
        var markup = ReadMainWindowMarkup();
        var appMarkup = ReadAppMarkup();

        Assert.NotNull(app.Resources);
        Assert.Contains("x:Class=\"DotNetScaffoldStudio.App.App\"", appMarkup, StringComparison.Ordinal);
        Assert.Contains("Strings.zh-TW.axaml", appMarkup, StringComparison.Ordinal);
        Assert.Contains("x:Class=\"DotNetScaffoldStudio.App.MainWindow\"", markup, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding RequestExecutionCommand}\"", markup, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding ConfirmExecutionCommand}\"", markup, StringComparison.Ordinal);
    }

    [Fact]
    public void WorkspaceAndNavigation_PreserveTargetAcrossEveryPrimaryPage()
    {
        var viewModel = CreateViewModel(new FakeExecutionService());
        viewModel.UseDemoWorkspaceCommand.Execute(null);
        var workspacePath = viewModel.WorkspacePath;
        var selectedProject = viewModel.SelectedProject;

        foreach (var navigation in viewModel.NavigationItems)
        {
            viewModel.SelectedNavigation = navigation;

            Assert.Equal(workspacePath, viewModel.WorkspacePath);
            Assert.Same(selectedProject, viewModel.SelectedProject);
            Assert.NotNull(viewModel.SelectedNavigation);
        }

        viewModel.ToggleNavigationCommand.Execute(null);
        Assert.True(viewModel.IsNavigationCollapsed);
        viewModel.ToggleNavigationCommand.Execute(null);
        Assert.True(viewModel.IsNavigationExpanded);
    }

    [Fact]
    public void ParameterEditor_ReportsValidationBeforeExecutionAndKeepsPreviewSafe()
    {
        var viewModel = CreateViewModel(new FakeExecutionService());
        viewModel.SelectedNavigation = viewModel.NavigationItems.Single(item => item.Id == "project");
        viewModel.SelectedFeature = viewModel.VisibleFeatures.Single(feature => feature.Id == "webapi");
        Assert.NotNull(viewModel.ParameterEditor);
        var editor = viewModel.ParameterEditor!;
        var name = Assert.IsType<TextParameterFieldViewModel>(
            editor.Fields.Single(field => field.Definition.Id == "name"));

        name.TextValue = "invalid name";

        Assert.False(editor.IsValid);
        Assert.False(viewModel.RequestExecutionCommand.CanExecute(null));
        Assert.Contains("名稱格式", name.ErrorMessage, StringComparison.Ordinal);
        Assert.Contains("invalid name", viewModel.CommandPreviewText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConfirmationAndExecution_ShowSafeResultState()
    {
        var execution = new FakeExecutionService();
        var viewModel = CreateViewModel(execution);
        viewModel.UseDemoWorkspaceCommand.Execute(null);

        viewModel.RequestExecutionCommand.Execute(null);

        Assert.True(viewModel.IsConfirmationVisible);
        Assert.Equal(ExecutionStage.AwaitingConfirmation, viewModel.ExecutionStage);
        Assert.Equal(ConfirmationKind.General, viewModel.CurrentConfirmationKind);
        Assert.Equal(0, execution.CallCount);

        await viewModel.ConfirmExecutionCommand.ExecuteAsync(null);

        Assert.False(viewModel.IsConfirmationVisible);
        Assert.Equal(ExecutionStage.Succeeded, viewModel.ExecutionStage);
        Assert.True(viewModel.HasResult);
        Assert.Single(viewModel.ResultFiles);
        Assert.Single(viewModel.ExistingChanges);
        Assert.Contains("變更", viewModel.GitDifferenceSummary, StringComparison.Ordinal);
        Assert.Equal(1, execution.CallCount);
    }

    [Fact]
    public async Task Startup_WithCorruptedSettings_UsesDefaultsAndLeavesUiAvailable()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var settingsPath = Path.Combine(temporaryDirectory.Path, "settings.json");
        await File.WriteAllTextAsync(settingsPath, "{ invalid json");

        var result = await new LocalSettingsStore(settingsPath).LoadAsync(CancellationToken.None);
        var viewModel = CreateViewModel(new FakeExecutionService());
        viewModel.RestorePreferences(result.Settings.Preferences);
        var app = new global::DotNetScaffoldStudio.App.App();
        app.Initialize();

        Assert.Equal(LocalSettingsLoadStatus.RecoveredFromCorruptFile, result.Status);
        Assert.True(result.UsedDefaults);
        Assert.False(File.Exists(settingsPath));
        Assert.NotNull(result.QuarantinedPath);
        Assert.True(File.Exists(result.QuarantinedPath));
        Assert.Equal("component", viewModel.SelectedNavigation?.Id);
        Assert.False(viewModel.IsNavigationCollapsed);
        Assert.NotNull(app.Resources);
    }

    private static string ReadMainWindowMarkup()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = System.IO.Path.Combine(
                directory.FullName,
                "src",
                "DotNetScaffoldStudio.App",
                "MainWindow.axaml");
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }
        }

        throw new DirectoryNotFoundException("找不到 MainWindow.axaml 測試來源。");
    }

    private static string ReadAppMarkup()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = System.IO.Path.Combine(
                directory.FullName,
                "src",
                "DotNetScaffoldStudio.App",
                "App.axaml");
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }
        }

        throw new DirectoryNotFoundException("找不到 App.axaml 測試來源。");
    }

    private static MainWindowViewModel CreateViewModel(IDemoExecutionService execution) =>
        new(new FakeWorkspaceService(), execution);

    private sealed class FakeWorkspaceService : IWorkspaceService
    {
        public Task<IReadOnlyList<ProjectInfo>> ScanProjectsAsync(
            string path,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ProjectInfo>>([]);
    }

    private sealed class FakeExecutionService : IDemoExecutionService
    {
        public int CallCount { get; private set; }

        public Task<DemoExecutionResult> ExecuteAsync(
            CommandPreview command,
            IProgress<string> progress,
            CancellationToken cancellationToken)
        {
            CallCount++;
            progress.Report("模擬輸出");
            return Task.FromResult(new DemoExecutionResult(
                true,
                "示意產生成功",
                "完成",
                ["模擬輸出"],
                ["Controllers/OrdersController.cs"],
                ["M Program.cs"],
                "demo/main",
                "執行前已有 1 項變更；執行後新增 1 項差異。"));
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"dotnet-scaffold-studio-ui-smoke-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
