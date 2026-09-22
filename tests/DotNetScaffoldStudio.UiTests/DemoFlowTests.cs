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
    public async Task DemoExecution_RequiresConfirmationAndReturnsSimulatedChanges()
    {
        var viewModel = new MainWindowViewModel(new FakeWorkspaceService(), new FakeExecutionService());

        viewModel.RequestExecutionCommand.Execute(null);
        Assert.True(viewModel.IsConfirmationVisible);

        await viewModel.ConfirmExecutionCommand.ExecuteAsync(null);

        Assert.True(viewModel.HasResult);
        Assert.Equal("示意產生成功", viewModel.ResultTitle);
        Assert.Single(viewModel.ResultFiles);
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
        public Task<DemoExecutionResult> ExecuteAsync(
            CommandPreview command,
            IProgress<string> progress,
            CancellationToken cancellationToken) =>
            Task.FromResult(new DemoExecutionResult(true, "示意產生成功", "完成", [], ["A Controllers/OrdersController.cs"]));
    }
}
