using DotNetScaffoldStudio.Application;
using DotNetScaffoldStudio.Domain;
using DotNetScaffoldStudio.Infrastructure;

namespace DotNetScaffoldStudio.IntegrationTests;

public sealed class LocalSettingsRestoreTests
{
    [Fact]
    public async Task Restart_RestoresRecentWorkspaceProjectWindowAndPreferences()
    {
        var root = CreateTemporaryDirectory();
        var workspacePath = Path.Combine(root, "workspace");
        Directory.CreateDirectory(workspacePath);
        var project = new ProjectInfo(
            "Api",
            Path.Combine(workspacePath, "Api.csproj"),
            "net10.0",
            "Microsoft.NET.Sdk.Web",
            "Api.csproj");
        var settingsPath = Path.Combine(root, "settings.json");

        try
        {
            var settings = new LocalSettingsDocument(
                LocalSettingsDocument.CurrentSchemaVersion,
                [new RecentWorkspaceEntry(workspacePath, project.Path)],
                new LocalWindowSettings(1440, 900, 120, 80, true),
                new LocalSettingsPreferences("settings", true));
            var firstStore = new LocalSettingsStore(settingsPath);
            await firstStore.SaveAsync(settings, CancellationToken.None);

            var loaded = await new LocalSettingsStore(settingsPath).LoadAsync(CancellationToken.None);
            var viewModel = new MainWindowViewModel(
                new FakeWorkspaceService(workspacePath, [project]),
                new FakeExecutionService());
            viewModel.RestorePreferences(loaded.Settings.Preferences);
            var session = new LocalSettingsSession(loaded.Settings);

            var restored = await session.RestoreMostRecentWorkspaceAsync(
                viewModel,
                path => Directory.Exists(path) || File.Exists(path),
                CancellationToken.None);

            Assert.True(restored);
            Assert.Equal(workspacePath, viewModel.WorkspacePath);
            Assert.Same(project, viewModel.SelectedProject);
            Assert.Equal("settings", viewModel.SelectedNavigation?.Id);
            Assert.True(viewModel.IsNavigationCollapsed);
            Assert.Equal(new LocalWindowSettings(1440, 900, 120, 80, true), loaded.Settings.Window);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Restart_RemovesInvalidRecentWorkspaceAndKeepsOtherEntries()
    {
        var root = CreateTemporaryDirectory();
        var validWorkspacePath = Path.Combine(root, "valid");
        Directory.CreateDirectory(validWorkspacePath);
        var invalidWorkspacePath = Path.Combine(root, "missing");
        var settingsPath = Path.Combine(root, "settings.json");

        try
        {
            var validProject = new ProjectInfo(
                "Api",
                Path.Combine(validWorkspacePath, "Api.csproj"),
                "net10.0",
                "Microsoft.NET.Sdk.Web",
                "Api.csproj");
            var settings = new LocalSettingsDocument(
                LocalSettingsDocument.CurrentSchemaVersion,
                [
                    new RecentWorkspaceEntry(invalidWorkspacePath, null),
                    new RecentWorkspaceEntry(validWorkspacePath, validProject.Path)
                ]);
            var store = new LocalSettingsStore(settingsPath);
            await store.SaveAsync(settings, CancellationToken.None);

            var loaded = await store.LoadAsync(CancellationToken.None);
            var viewModel = new MainWindowViewModel(
                new FakeWorkspaceService(validWorkspacePath, [validProject]),
                new FakeExecutionService());
            var session = new LocalSettingsSession(loaded.Settings);

            var restored = await session.RestoreMostRecentWorkspaceAsync(
                viewModel,
                path => Directory.Exists(path) || File.Exists(path),
                CancellationToken.None);
            await store.SaveAsync(
                session.Capture(viewModel, LocalWindowSettings.Default),
                CancellationToken.None);

            var persisted = await new LocalSettingsStore(settingsPath).LoadAsync(CancellationToken.None);

            Assert.True(restored);
            Assert.DoesNotContain(persisted.Settings.RecentWorkspaces, entry =>
                string.Equals(entry.WorkspacePath, invalidWorkspacePath, StringComparison.Ordinal));
            Assert.Contains(persisted.Settings.RecentWorkspaces, entry =>
                string.Equals(entry.WorkspacePath, validWorkspacePath, StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"dotnet-scaffold-studio-settings-restore-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class FakeWorkspaceService : IWorkspaceService
    {
        private readonly string _workspacePath;
        private readonly IReadOnlyList<ProjectInfo> _projects;

        public FakeWorkspaceService(string workspacePath, IReadOnlyList<ProjectInfo> projects)
        {
            _workspacePath = workspacePath;
            _projects = projects;
        }

        public Task<IReadOnlyList<ProjectInfo>> ScanProjectsAsync(string path, CancellationToken cancellationToken) =>
            string.Equals(path, _workspacePath, StringComparison.Ordinal)
                ? Task.FromResult(_projects)
                : Task.FromException<IReadOnlyList<ProjectInfo>>(new DirectoryNotFoundException());
    }

    private sealed class FakeExecutionService : IDemoExecutionService
    {
        public Task<DemoExecutionResult> ExecuteAsync(
            CommandPreview command,
            IProgress<string> progress,
            CancellationToken cancellationToken) =>
            Task.FromResult(new DemoExecutionResult(true, string.Empty, string.Empty, [], []));
    }
}
