using DotNetScaffoldStudio.Application;
using DotNetScaffoldStudio.Domain;

namespace DotNetScaffoldStudio.UiTests;

public sealed class DemoFlowTests
{
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
