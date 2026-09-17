using DotNetScaffoldStudio.Application;
using DotNetScaffoldStudio.Domain;

namespace DotNetScaffoldStudio.Infrastructure;

public sealed class WorkspacePathResolver
{
    public WorkspaceSelection Resolve(string selectedPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(selectedPath);
        var fullPath = Path.GetFullPath(selectedPath);

        if (Directory.Exists(fullPath))
        {
            EnsureDirectoryReadable(fullPath);
            return new WorkspaceSelection(fullPath, ResolveFinalTarget(fullPath), WorkspaceSelectionKind.Folder);
        }

        if (!File.Exists(fullPath))
        {
            throw new DirectoryNotFoundException("選擇的工作區不存在或無法讀取。");
        }

        var extension = Path.GetExtension(fullPath);
        var kind = extension.ToLowerInvariant() switch
        {
            ".sln" => WorkspaceSelectionKind.Solution,
            ".slnx" => WorkspaceSelectionKind.SolutionXml,
            _ => throw new ArgumentException("工作區檔案必須是 .sln 或 .slnx。", nameof(selectedPath))
        };

        try
        {
            using var stream = File.Open(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new UnauthorizedAccessException("選擇的 Solution 無法讀取。", exception);
        }

        var root = Path.GetDirectoryName(fullPath)
            ?? throw new ArgumentException("無法判斷 Solution 所在目錄。", nameof(selectedPath));
        EnsureDirectoryReadable(root);
        return new WorkspaceSelection(fullPath, ResolveFinalTarget(root), kind);
    }

    public static bool IsPathWithin(string workspaceRoot, string candidatePath)
    {
        var realRoot = ResolveFinalTarget(workspaceRoot);
        var realCandidate = ResolveFinalTarget(candidatePath);
        var relative = Path.GetRelativePath(realRoot, realCandidate);
        return !Path.IsPathRooted(relative) &&
               !string.Equals(relative, "..", StringComparison.Ordinal) &&
               !relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal);
    }

    private static string ResolveFinalTarget(string path)
    {
        var fullPath = Path.GetFullPath(path);
        FileSystemInfo info = Directory.Exists(fullPath) ? new DirectoryInfo(fullPath) : new FileInfo(fullPath);
        return info.ResolveLinkTarget(returnFinalTarget: true)?.FullName ?? fullPath;
    }

    private static void EnsureDirectoryReadable(string path)
    {
        try
        {
            _ = Directory.EnumerateFileSystemEntries(path).Take(1).ToArray();
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new UnauthorizedAccessException("選擇的工作區無法讀取。", exception);
        }
    }
}

public sealed class TargetProjectValidator : ITargetProjectValidator
{
    public TargetValidationResult Validate(string workspaceRoot, string projectPath, string commandWorkingDirectory)
    {
        if (!Directory.Exists(workspaceRoot))
        {
            return new TargetValidationResult(false, "工作區已不存在，請重新選擇。");
        }

        if (!File.Exists(projectPath) || !string.Equals(Path.GetExtension(projectPath), ".csproj", StringComparison.OrdinalIgnoreCase))
        {
            return new TargetValidationResult(false, "目標專案已移動或刪除，請重新掃描並選擇專案。");
        }

        if (!WorkspacePathResolver.IsPathWithin(workspaceRoot, projectPath))
        {
            return new TargetValidationResult(false, "目標專案不在目前工作區內。");
        }

        if (!string.Equals(Path.GetFullPath(workspaceRoot), Path.GetFullPath(commandWorkingDirectory), StringComparison.Ordinal))
        {
            return new TargetValidationResult(false, "命令工作目錄與畫面顯示的工作區不一致。");
        }

        return TargetValidationResult.Valid;
    }
}
