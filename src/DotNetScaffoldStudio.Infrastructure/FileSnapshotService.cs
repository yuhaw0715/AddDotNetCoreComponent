using System.Collections.ObjectModel;
using DotNetScaffoldStudio.Application;
using DotNetScaffoldStudio.Domain;

namespace DotNetScaffoldStudio.Infrastructure;

public sealed class FileSnapshotService : IFileSnapshotService
{
    private static readonly HashSet<string> ExcludedDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", "bin", "obj", "node_modules", "artifacts"
    };

    public Task<FileSnapshot> CaptureAsync(string workspaceRoot, CancellationToken cancellationToken)
    {
        var root = Path.GetFullPath(workspaceRoot);
        var files = new Dictionary<string, FileMetadata>(StringComparer.Ordinal);
        var directories = new Stack<string>();
        directories.Push(root);

        while (directories.TryPop(out var directory))
        {
            cancellationToken.ThrowIfCancellationRequested();
            FileSystemInfo[] entries;
            try
            {
                entries = new DirectoryInfo(directory).GetFileSystemInfos();
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }
            catch (DirectoryNotFoundException)
            {
                continue;
            }

            foreach (var entry in entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (entry.Attributes.HasFlag(FileAttributes.ReparsePoint))
                {
                    continue;
                }

                if (entry is DirectoryInfo child && !ExcludedDirectories.Contains(child.Name))
                {
                    directories.Push(child.FullName);
                }
                else if (entry is FileInfo file)
                {
                    files[Path.GetRelativePath(root, file.FullName)] = new FileMetadata(file.Length, file.LastWriteTimeUtc);
                }
            }
        }

        return Task.FromResult(new FileSnapshot(root, new ReadOnlyDictionary<string, FileMetadata>(files)));
    }

    public IReadOnlyList<FileChange> Compare(FileSnapshot before, FileSnapshot after)
    {
        if (!string.Equals(before.RootPath, after.RootPath, StringComparison.Ordinal))
        {
            throw new ArgumentException("檔案快照必須屬於相同工作區。", nameof(after));
        }

        var changes = new List<FileChange>();
        foreach (var path in before.Files.Keys.Except(after.Files.Keys, StringComparer.Ordinal))
        {
            changes.Add(new FileChange(FileChangeKind.Deleted, path));
        }

        foreach (var path in after.Files.Keys.Except(before.Files.Keys, StringComparer.Ordinal))
        {
            changes.Add(new FileChange(FileChangeKind.Added, path));
        }

        foreach (var path in before.Files.Keys.Intersect(after.Files.Keys, StringComparer.Ordinal))
        {
            if (before.Files[path] != after.Files[path])
            {
                changes.Add(new FileChange(FileChangeKind.Modified, path));
            }
        }

        return changes.OrderBy(change => change.RelativePath, StringComparer.Ordinal).ToArray();
    }
}
