using System.Xml.Linq;
using DotNetScaffoldStudio.Application;
using DotNetScaffoldStudio.Domain;

namespace DotNetScaffoldStudio.Infrastructure;

public sealed class WorkspaceService : IWorkspaceService
{
    private static readonly HashSet<string> ExcludedSegments = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git",
        "bin",
        "obj"
    };

    public async Task<IReadOnlyList<ProjectInfo>> ScanProjectsAsync(string path, CancellationToken cancellationToken)
    {
        var selection = new WorkspacePathResolver().Resolve(path);
        var root = selection.RootPath;

        var projects = new List<ProjectInfo>();
        foreach (var projectPath in EnumerateProjectFiles(root, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await using var stream = File.OpenRead(projectPath);
            var document = await XDocument.LoadAsync(stream, LoadOptions.None, cancellationToken);
            var rootElement = document.Root;
            var sdk = rootElement?.Attribute("Sdk")?.Value;
            var targetFramework = rootElement?
                .Descendants()
                .FirstOrDefault(element => element.Name.LocalName is "TargetFramework" or "TargetFrameworks")?
                .Value;
            projects.Add(new ProjectInfo(
                Path.GetFileNameWithoutExtension(projectPath),
                projectPath,
                string.IsNullOrWhiteSpace(targetFramework) ? "未指定" : targetFramework,
                string.IsNullOrWhiteSpace(sdk) ? "Microsoft.NET.Sdk" : sdk,
                Path.GetRelativePath(root, projectPath)));
        }

        return projects.OrderBy(project => project.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static IEnumerable<string> EnumerateProjectFiles(string root, CancellationToken cancellationToken)
    {
        var pending = new Stack<string>();
        pending.Push(root);

        while (pending.TryPop(out var directory))
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
                if (entry is DirectoryInfo childDirectory)
                {
                    if (ExcludedSegments.Contains(childDirectory.Name) ||
                        childDirectory.Attributes.HasFlag(FileAttributes.Hidden) ||
                        childDirectory.Attributes.HasFlag(FileAttributes.ReparsePoint))
                    {
                        continue;
                    }

                    pending.Push(childDirectory.FullName);
                }
                else if (entry is FileInfo file &&
                         string.Equals(file.Extension, ".csproj", StringComparison.OrdinalIgnoreCase) &&
                         !file.Attributes.HasFlag(FileAttributes.ReparsePoint))
                {
                    yield return file.FullName;
                }
            }
        }
    }
}
