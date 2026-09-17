namespace DotNetScaffoldStudio.Domain;

public sealed record GitFileChange(string Status, string Path);

public sealed record GitWorkspaceState(
    bool IsRepository,
    string? Branch,
    IReadOnlyList<GitFileChange> Changes);

public sealed record FileMetadata(long Length, DateTimeOffset LastWriteTimeUtc);

public sealed record FileSnapshot(string RootPath, IReadOnlyDictionary<string, FileMetadata> Files);

public enum FileChangeKind
{
    Added,
    Modified,
    Deleted
}

public sealed record FileChange(FileChangeKind Kind, string RelativePath);

public sealed record ExecutionDifferenceSummary(
    IReadOnlyList<GitFileChange> ExistingGitChanges,
    IReadOnlyList<GitFileChange> CurrentGitChanges,
    IReadOnlyList<FileChange> CommandFileChanges);

public sealed record CommandExecutionResult(
    CommandResult Command,
    ExecutionDifferenceSummary? Differences);
