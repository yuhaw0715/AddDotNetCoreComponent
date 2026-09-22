using DotNetScaffoldStudio.Application;
using DotNetScaffoldStudio.Domain;

namespace DotNetScaffoldStudio.UiTests;

public sealed class DemoFlowTests
{
    [Fact]
    public void Navigation_ContainsAllRequiredGroupsAndPreservesWorkspaceState()
    {
        var viewModel = new MainWindowViewModel(new FakeWorkspaceService(), new FakeExecutionService());
        var expectedIds = new[] { "home", "project", "component", "scaffolding", "efcore", "custom", "history", "settings" };

        Assert.Equal(expectedIds, viewModel.NavigationItems.Select(item => item.Id));
        Assert.All(viewModel.NavigationItems, item => Assert.False(string.IsNullOrWhiteSpace(item.Glyph)));

        viewModel.UseDemoWorkspaceCommand.Execute(null);
        var workspacePath = viewModel.WorkspacePath;
        var selectedProject = viewModel.SelectedProject;

        foreach (var item in viewModel.NavigationItems)
        {
            viewModel.SelectedNavigation = item;

            Assert.Equal(workspacePath, viewModel.WorkspacePath);
            Assert.Same(selectedProject, viewModel.SelectedProject);
        }
    }

    [Fact]
    public void Navigation_CanCollapseAndExpandWithoutChangingSelection()
    {
        var viewModel = new MainWindowViewModel(new FakeWorkspaceService(), new FakeExecutionService());
        var selectedNavigation = viewModel.SelectedNavigation;

        viewModel.ToggleNavigationCommand.Execute(null);

        Assert.True(viewModel.IsNavigationCollapsed);
        Assert.False(viewModel.IsNavigationExpanded);
        Assert.Equal(72, viewModel.NavigationPaneWidth);
        Assert.Same(selectedNavigation, viewModel.SelectedNavigation);

        viewModel.ToggleNavigationCommand.Execute(null);

        Assert.False(viewModel.IsNavigationCollapsed);
        Assert.True(viewModel.IsNavigationExpanded);
        Assert.Equal(270, viewModel.NavigationPaneWidth);
    }

    [Fact]
    public void Navigation_SelectingScaffoldingShowsControllerFlow()
    {
        var viewModel = new MainWindowViewModel(new FakeWorkspaceService(), new FakeExecutionService());

        viewModel.SelectedNavigation = viewModel.NavigationItems.Single(item => item.Id == "scaffolding");

        Assert.Contains(viewModel.VisibleFeatures, feature => feature.Id == "controller");
        Assert.Contains("aspnet-codegenerator", viewModel.CommandPreviewText, StringComparison.Ordinal);
    }

    [Fact]
    public void FeatureSearch_FiltersByNameAndShowsDependencyState()
    {
        var viewModel = new MainWindowViewModel(new FakeWorkspaceService(), new FakeExecutionService());

        viewModel.SelectedNavigation = viewModel.NavigationItems.Single(item => item.Id == "scaffolding");
        viewModel.FeatureSearchText = "minimal";

        var feature = Assert.Single(viewModel.VisibleFeatures);
        Assert.Equal("minimalapi", feature.Id);
        Assert.Contains("Scaffolding", feature.DependencySummary, StringComparison.Ordinal);
        Assert.Equal("可用", feature.AvailabilityLabel);

        viewModel.FeatureSearchText = "does-not-exist";

        Assert.Empty(viewModel.VisibleFeatures);
        Assert.False(viewModel.HasVisibleFeatures);
        Assert.Null(viewModel.SelectedFeature);
    }

    [Fact]
    public void FeatureCatalog_WindowsTemplatesRemainVisibleButUnavailableOnMacOS()
    {
        var viewModel = new MainWindowViewModel(new FakeWorkspaceService(), new FakeExecutionService());

        viewModel.SelectedNavigation = viewModel.NavigationItems.Single(item => item.Id == "project");

        var windowsFeatures = viewModel.VisibleFeatures.Where(feature => feature.Id is "wpf" or "winforms").ToArray();

        Assert.Equal(2, windowsFeatures.Length);
        Assert.All(windowsFeatures, feature => Assert.False(feature.IsAvailable));
        Assert.Contains("不支援 macOS", windowsFeatures[0].AvailabilityReason, StringComparison.Ordinal);
    }

    [Fact]
    public void ParameterEditor_UsesSchemaAndKeepsAdvancedValues()
    {
        var viewModel = new MainWindowViewModel(new FakeWorkspaceService(), new FakeExecutionService());
        Assert.NotNull(viewModel.ParameterEditor);
        var editor = viewModel.ParameterEditor!;

        Assert.Contains(editor.VisibleFields, field => field.Definition.Id == "name");
        Assert.DoesNotContain(editor.VisibleFields, field => field.Definition.Id == "output");

        var nameField = Assert.IsType<TextParameterFieldViewModel>(editor.Fields.Single(field => field.Definition.Id == "name"));
        nameField.TextValue = "1 invalid";

        Assert.False(editor.IsValid);
        Assert.False(viewModel.RequestExecutionCommand.CanExecute(null));
        Assert.Contains("名稱格式", nameField.ErrorMessage, StringComparison.Ordinal);

        nameField.TextValue = "InvoicesController";
        editor.ShowAdvanced = true;
        var outputField = Assert.IsType<TextParameterFieldViewModel>(editor.Fields.Single(field => field.Definition.Id == "output"));
        outputField.TextValue = "Generated";
        editor.ShowAdvanced = false;

        Assert.Equal("Generated", outputField.TextValue);
        Assert.DoesNotContain(outputField, editor.VisibleFields);
        Assert.Contains("InvoicesController", viewModel.CommandPreviewText, StringComparison.Ordinal);
        Assert.Contains("Generated", viewModel.CommandPreviewText, StringComparison.Ordinal);
        Assert.True(editor.IsValid);
    }

    [Fact]
    public void ParameterEditor_UpdatesScaffoldingPreviewAndMasksSecretFields()
    {
        var viewModel = new MainWindowViewModel(new FakeWorkspaceService(), new FakeExecutionService());
        viewModel.UseDemoWorkspaceCommand.Execute(null);
        viewModel.SelectedNavigation = viewModel.NavigationItems.Single(item => item.Id == "scaffolding");
        viewModel.SelectedFeature = viewModel.VisibleFeatures.Single(feature => feature.Id == "controller");

        Assert.NotNull(viewModel.ParameterEditor);
        var scaffoldingEditor = viewModel.ParameterEditor!;
        var variant = Assert.IsType<EnumerationParameterFieldViewModel>(scaffoldingEditor.Fields.Single(field => field.Definition.Id == "variant"));

        Assert.Equal("含讀寫動作", variant.SelectedValue);
        variant.SelectedValue = "REST API";

        Assert.Contains("--restWithNoViews", viewModel.CommandPreviewText, StringComparison.Ordinal);

        viewModel.SelectedNavigation = viewModel.NavigationItems.Single(item => item.Id == "efcore");
        viewModel.SelectedFeature = viewModel.VisibleFeatures.Single(feature => feature.Id == "dbcontext-scaffold");
        Assert.NotNull(viewModel.ParameterEditor);
        var efEditor = viewModel.ParameterEditor!;
        var secret = Assert.IsType<TextParameterFieldViewModel>(efEditor.Fields.Single(field => field.Definition.Id == "connection"));

        secret.TextValue = "Server=private.example;Password=secret";

        Assert.Equal('●', secret.PasswordChar);
        Assert.DoesNotContain("private.example", viewModel.CommandPreviewText, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", viewModel.CommandPreviewText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DemoExecution_RequiresConfirmationAndReturnsSimulatedChanges()
    {
        var viewModel = new MainWindowViewModel(new FakeWorkspaceService(), new FakeExecutionService());

        viewModel.RequestExecutionCommand.Execute(null);
        Assert.True(viewModel.IsConfirmationVisible);

        await viewModel.ConfirmExecutionCommand.ExecuteAsync(null);

        Assert.True(viewModel.HasResult);
        Assert.Equal("示意產生成功", viewModel.ResultTitle);
        Assert.Single(viewModel.ResultFiles);
        Assert.Single(viewModel.ExistingChanges);
        Assert.Equal(ExecutionStage.Succeeded, viewModel.ExecutionStage);
        Assert.Contains("變更", viewModel.GitDifferenceSummary, StringComparison.Ordinal);
        Assert.True(viewModel.CanOpenResultInFinder);
    }

    [Fact]
    public async Task Execution_FailureShowsFailedStageAndPreservesDifferences()
    {
        var viewModel = new MainWindowViewModel(new FakeWorkspaceService(), new FakeExecutionService(false));

        viewModel.RequestExecutionCommand.Execute(null);
        await viewModel.ConfirmExecutionCommand.ExecuteAsync(null);

        Assert.True(viewModel.HasResult);
        Assert.Equal(ExecutionStage.Failed, viewModel.ExecutionStage);
        Assert.Single(viewModel.ResultFiles);
        Assert.Single(viewModel.ExistingChanges);
    }

    [Fact]
    public async Task Execution_CancelShowsCancelledStageAndDoesNotAssumeNoChanges()
    {
        var execution = new BlockingExecutionService();
        var viewModel = new MainWindowViewModel(new FakeWorkspaceService(), execution);

        viewModel.RequestExecutionCommand.Execute(null);
        var executionTask = viewModel.ConfirmExecutionCommand.ExecuteAsync(null);
        await execution.Started.Task;

        viewModel.CancelExecutionCommand.Execute(null);
        await executionTask;

        Assert.Equal(ExecutionStage.Cancelled, viewModel.ExecutionStage);
        Assert.True(viewModel.HasResult);
        Assert.Contains("重新掃描", viewModel.ResultSummary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Execution_CanRevealOutputThroughInjectedFinderService()
    {
        var finder = new FakeFileRevealService();
        var viewModel = new MainWindowViewModel(
            new FakeWorkspaceService(),
            new FakeExecutionService(),
            new ParameterValidator(),
            finder);

        viewModel.RequestExecutionCommand.Execute(null);
        await viewModel.ConfirmExecutionCommand.ExecuteAsync(null);
        await viewModel.OpenResultInFinderCommand.ExecuteAsync(null);

        Assert.NotNull(finder.RevealedPath);
        Assert.Contains("Controllers", finder.RevealedPath, StringComparison.Ordinal);
    }

    [Fact]
    public void Confirmation_UsesSpecificDialogForOverwriteAndCancelDoesNotExecute()
    {
        var execution = new FakeExecutionService();
        var viewModel = new MainWindowViewModel(new FakeWorkspaceService(), execution);

        viewModel.SelectedNavigation = viewModel.NavigationItems.Single(item => item.Id == "project");
        viewModel.SelectedFeature = viewModel.VisibleFeatures.Single(feature => feature.Id == "webapi");
        var editor = viewModel.ParameterEditor!;
        editor.ShowAdvanced = true;
        var force = Assert.IsType<BooleanParameterFieldViewModel>(editor.Fields.Single(field => field.Definition.Id == "force"));
        force.IsChecked = true;

        Assert.Equal(ConfirmationKind.FileOverwrite, viewModel.CurrentConfirmationKind);
        viewModel.RequestExecutionCommand.Execute(null);
        Assert.Equal("確認覆寫既有檔案", viewModel.ConfirmationTitle);
        Assert.True(viewModel.IsConfirmationVisible);

        viewModel.CancelConfirmationCommand.Execute(null);

        Assert.False(viewModel.IsConfirmationVisible);
        Assert.Equal(0, execution.CallCount);
    }

    [Fact]
    public void Confirmation_DistinguishesToolInstallationAndDatabaseUpdate()
    {
        var viewModel = new MainWindowViewModel(new FakeWorkspaceService(), new FakeExecutionService());

        viewModel.SelectedNavigation = viewModel.NavigationItems.Single(item => item.Id == "custom");
        viewModel.SelectedFeature = viewModel.VisibleFeatures.Single(feature => feature.Id == "tool-install");
        Assert.Equal(ConfirmationKind.ToolInstallation, viewModel.CurrentConfirmationKind);
        Assert.Contains("dotnet-tools.json", viewModel.ConfirmationDetails, StringComparison.Ordinal);

        viewModel.UseDemoWorkspaceCommand.Execute(null);
        viewModel.SelectedNavigation = viewModel.NavigationItems.Single(item => item.Id == "efcore");
        viewModel.SelectedFeature = viewModel.VisibleFeatures.Single(feature => feature.Id == "database-update");

        Assert.Equal(ConfirmationKind.DatabaseUpdate, viewModel.CurrentConfirmationKind);
        Assert.Contains("連線來源", viewModel.ConfirmationDetails, StringComparison.Ordinal);
        Assert.DoesNotContain("Password", viewModel.ConfirmationDetails, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WorkspaceScan_EmptyWorkspaceKeepsTemplateFlowAvailable()
    {
        var viewModel = new MainWindowViewModel(
            new FakeWorkspaceService([]),
            new FakeExecutionService());

        await viewModel.LoadWorkspaceAsync("/tmp/empty-workspace");

        Assert.Equal("/tmp/empty-workspace", viewModel.WorkspacePath);
        Assert.True(viewModel.IsProjectsEmpty);
        Assert.False(viewModel.HasProjects);
        Assert.Null(viewModel.SelectedProject);
        Assert.Contains("找不到", viewModel.StatusMessage, StringComparison.Ordinal);
        Assert.True(viewModel.CanExecuteSelectedFeature);

        viewModel.SelectedNavigation = viewModel.NavigationItems.Single(item => item.Id == "scaffolding");

        Assert.False(viewModel.CanExecuteSelectedFeature);
    }

    [Fact]
    public async Task WorkspaceScan_SingleProjectAutomaticallySelectsTarget()
    {
        var project = new ProjectInfo("Api", "/tmp/single/Api.csproj", "net10.0", "Microsoft.NET.Sdk.Web", "Api.csproj");
        var viewModel = new MainWindowViewModel(
            new FakeWorkspaceService([project]),
            new FakeExecutionService());

        await viewModel.LoadWorkspaceAsync("/tmp/single");

        Assert.Single(viewModel.Projects);
        Assert.Same(project, viewModel.SelectedProject);
        Assert.False(viewModel.RequiresProjectSelection);
    }

    [Fact]
    public async Task WorkspaceScan_MultipleProjectsRequiresExplicitTargetSelection()
    {
        var projects = new[]
        {
            new ProjectInfo("Api", "/tmp/multi/Api/Api.csproj", "net10.0", "Microsoft.NET.Sdk.Web", "Api/Api.csproj"),
            new ProjectInfo("Tests", "/tmp/multi/Tests/Tests.csproj", "net10.0", "Microsoft.NET.Sdk", "Tests/Tests.csproj")
        };
        var viewModel = new MainWindowViewModel(
            new FakeWorkspaceService(projects),
            new FakeExecutionService());

        await viewModel.LoadWorkspaceAsync("/tmp/multi");

        Assert.Equal(2, viewModel.Projects.Count);
        Assert.Null(viewModel.SelectedProject);
        Assert.True(viewModel.RequiresProjectSelection);

        viewModel.SelectedNavigation = viewModel.NavigationItems.Single(item => item.Id == "scaffolding");

        Assert.False(viewModel.CanExecuteSelectedFeature);

        viewModel.SelectedProject = projects[1];

        Assert.False(viewModel.RequiresProjectSelection);
        Assert.Contains(projects[1].Path, viewModel.CommandPreviewText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WorkspaceScan_FailureKeepsPreviousWorkspaceAndSelection()
    {
        var project = new ProjectInfo("Api", "/tmp/valid/Api.csproj", "net10.0", "Microsoft.NET.Sdk.Web");
        var viewModel = new MainWindowViewModel(
            new FailOnSecondWorkspaceService(project),
            new FakeExecutionService());

        await viewModel.LoadWorkspaceAsync("/tmp/valid");
        var previousProject = viewModel.SelectedProject;
        await viewModel.LoadWorkspaceAsync("/tmp/invalid");

        Assert.Equal("/tmp/valid", viewModel.WorkspacePath);
        Assert.Same(previousProject, viewModel.SelectedProject);
        Assert.Contains("無法載入", viewModel.StatusMessage, StringComparison.Ordinal);
    }

    private sealed class FakeWorkspaceService : IWorkspaceService
    {
        private readonly IReadOnlyList<ProjectInfo> _projects;
        private readonly Exception? _exception;

        public FakeWorkspaceService(IReadOnlyList<ProjectInfo>? projects = null, Exception? exception = null)
        {
            _projects = projects ?? [];
            _exception = exception;
        }

        public Task<IReadOnlyList<ProjectInfo>> ScanProjectsAsync(string path, CancellationToken cancellationToken) =>
            _exception is null
                ? Task.FromResult(_projects)
                : Task.FromException<IReadOnlyList<ProjectInfo>>(_exception);
    }

    private sealed class FailOnSecondWorkspaceService : IWorkspaceService
    {
        private readonly ProjectInfo _project;
        private int _calls;

        public FailOnSecondWorkspaceService(ProjectInfo project) => _project = project;

        public Task<IReadOnlyList<ProjectInfo>> ScanProjectsAsync(string path, CancellationToken cancellationToken)
        {
            _calls++;
            return _calls == 1
                ? Task.FromResult<IReadOnlyList<ProjectInfo>>([_project])
                : Task.FromException<IReadOnlyList<ProjectInfo>>(new InvalidOperationException("路徑無法讀取"));
        }
    }

    private sealed class FakeExecutionService : IDemoExecutionService
    {
        private readonly bool _succeeded;
        public int CallCount { get; private set; }

        public FakeExecutionService(bool succeeded = true) => _succeeded = succeeded;

        public Task<DemoExecutionResult> ExecuteAsync(
            CommandPreview command,
            IProgress<string> progress,
            CancellationToken cancellationToken) =>
            ExecuteCoreAsync();

        private Task<DemoExecutionResult> ExecuteCoreAsync()
        {
            CallCount++;
            return Task.FromResult(new DemoExecutionResult(
                _succeeded,
                _succeeded ? "示意產生成功" : "示意產生失敗",
                _succeeded ? "完成" : "CLI 回報失敗，保留實際差異。",
                [],
                ["A Controllers/OrdersController.cs"],
                ["M  Program.cs"],
                "demo/main",
                "執行前已有 1 項變更；執行後新增 1 項差異。"));
        }
    }

    private sealed class BlockingExecutionService : IDemoExecutionService
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<DemoExecutionResult> ExecuteAsync(
            CommandPreview command,
            IProgress<string> progress,
            CancellationToken cancellationToken)
        {
            Started.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new DemoExecutionResult(true, "不會到達", string.Empty, [], []);
        }
    }

    private sealed class FakeFileRevealService : IFileRevealService
    {
        public string? RevealedPath { get; private set; }

        public Task<FileRevealResult> RevealAsync(string path, CancellationToken cancellationToken)
        {
            RevealedPath = path;
            return Task.FromResult(new FileRevealResult(true, "已顯示"));
        }
    }
}
