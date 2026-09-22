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

    private sealed class FakeWorkspaceService : IWorkspaceService
    {
        public Task<IReadOnlyList<ProjectInfo>> ScanProjectsAsync(string path, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ProjectInfo>>([]);
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
