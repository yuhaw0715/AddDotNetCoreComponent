using DotNetScaffoldStudio.Domain;

namespace DotNetScaffoldStudio.Application;

public sealed class ExpectedOutputConflictChecker
{
    public OutputConflictResult Check(
        CommandRequest request,
        IReadOnlyCollection<string> existingRelativePaths)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(existingRelativePaths);

        var existing = existingRelativePaths
            .Select(path => NormalizeRelativePath(request.WorkingDirectory, path))
            .ToHashSet(StringComparer.Ordinal);
        var conflicts = request.ExpectedOutputs
            .Select(path => NormalizeRelativePath(request.WorkingDirectory, path))
            .Where(expected => existing.Any(path => PathsOverlap(expected, path)))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        var forceRequested = request.Arguments.Any(argument =>
            string.Equals(argument.Value, "--force", StringComparison.Ordinal));

        if (conflicts.Length == 0 && !forceRequested)
        {
            return new OutputConflictResult(
                OutputConflictStatus.NoConflict,
                [],
                "預期輸出沒有偵測到既有檔案或目錄衝突。");
        }

        if (forceRequested)
        {
            return new OutputConflictResult(
                OutputConflictStatus.RequiresConfirmation,
                conflicts,
                conflicts.Length == 0
                    ? "已明確選擇 force，執行前仍需確認可能覆寫工作區輸出。"
                    : "預期輸出已存在，只有在明確確認覆寫後才能執行。");
        }

        return new OutputConflictResult(
            OutputConflictStatus.Blocked,
            conflicts,
            "預期輸出已存在；請取消 force 或明確選擇覆寫並進入高風險確認流程。");
    }

    private static bool PathsOverlap(string expected, string existing) =>
        string.Equals(expected, existing, StringComparison.Ordinal) ||
        existing.StartsWith($"{expected}{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
        expected.StartsWith($"{existing}{Path.DirectorySeparatorChar}", StringComparison.Ordinal);

    private static string NormalizeRelativePath(string workingDirectory, string path)
    {
        var fullPath = Path.GetFullPath(path, workingDirectory);
        var relative = Path.GetRelativePath(workingDirectory, fullPath);
        if (Path.IsPathRooted(relative) ||
            string.Equals(relative, "..", StringComparison.Ordinal) ||
            relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
        {
            throw new ArgumentException("輸出衝突檢查路徑必須位於命令工作區內。", nameof(path));
        }

        return relative;
    }
}
