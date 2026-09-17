using DotNetScaffoldStudio.Domain;

namespace DotNetScaffoldStudio.Application;

public static class ExecutionDifferenceAggregator
{
    public static ExecutionDifferenceSummary Create(
        GitWorkspaceState beforeGit,
        GitWorkspaceState afterGit,
        IReadOnlyList<FileChange> fileChanges)
    {
        var existingKeys = beforeGit.Changes
            .Select(change => $"{change.Status}\u001f{change.Path}")
            .ToHashSet(StringComparer.Ordinal);
        var commandGitChanges = afterGit.Changes
            .Where(change => !existingKeys.Contains($"{change.Status}\u001f{change.Path}"))
            .ToArray();

        return new ExecutionDifferenceSummary(beforeGit.Changes, commandGitChanges, fileChanges);
    }
}
