using DotNetScaffoldStudio.Domain;

namespace DotNetScaffoldStudio.Application;

public interface ILocalSettingsStore
{
    string SettingsPath { get; }

    Task<LocalSettingsLoadResult> LoadAsync(CancellationToken cancellationToken);

    Task SaveAsync(LocalSettingsDocument settings, CancellationToken cancellationToken);
}

public interface IAtomicFileReplacer
{
    Task ReplaceAsync(
        string destinationPath,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken);
}

public sealed class LocalSettingsSession
{
    private readonly List<RecentWorkspaceEntry> _recentWorkspaces;

    public LocalSettingsSession(LocalSettingsDocument settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _recentWorkspaces = settings.RecentWorkspaces
            .Where(entry => !string.IsNullOrWhiteSpace(entry.WorkspacePath))
            .ToList();
    }

    public IReadOnlyList<RecentWorkspaceEntry> RecentWorkspaces => _recentWorkspaces.AsReadOnly();

    public void RemoveInvalidWorkspaces(Func<string, bool> isValidWorkspace)
    {
        ArgumentNullException.ThrowIfNull(isValidWorkspace);
        _recentWorkspaces.RemoveAll(entry => !isValidWorkspace(entry.WorkspacePath));
    }

    public async Task<bool> RestoreMostRecentWorkspaceAsync(
        MainWindowViewModel viewModel,
        Func<string, bool> isValidWorkspace,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(isValidWorkspace);

        for (var index = 0; index < _recentWorkspaces.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var entry = _recentWorkspaces[index];
            if (!isValidWorkspace(entry.WorkspacePath) ||
                !await viewModel.LoadWorkspaceAsync(entry.WorkspacePath, entry.LastProjectPath))
            {
                _recentWorkspaces.RemoveAt(index--);
                continue;
            }

            return true;
        }

        return false;
    }

    public LocalSettingsDocument Capture(MainWindowViewModel viewModel, LocalWindowSettings window)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(window);

        var currentWorkspace = viewModel.CaptureRecentWorkspace();
        if (currentWorkspace is not null)
        {
            RemoveWorkspace(currentWorkspace.WorkspacePath);
            _recentWorkspaces.Insert(0, currentWorkspace);
        }

        return CreateDocument(viewModel.CapturePreferences(), window);
    }

    public LocalSettingsDocument CreateDocument(
        LocalSettingsPreferences preferences,
        LocalWindowSettings window)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        ArgumentNullException.ThrowIfNull(window);

        return new LocalSettingsDocument(
            LocalSettingsDocument.CurrentSchemaVersion,
            _recentWorkspaces,
            window,
            preferences);
    }

    private void RemoveWorkspace(string workspacePath)
    {
        _recentWorkspaces.RemoveAll(entry => PathsEqual(entry.WorkspacePath, workspacePath));
    }

    private static bool PathsEqual(string left, string right)
    {
        try
        {
            left = Path.GetFullPath(left);
            right = Path.GetFullPath(right);
        }
        catch (ArgumentException)
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }

        return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }
}
