using System.ComponentModel;
using DotNetScaffoldStudio.Application;
using DotNetScaffoldStudio.Domain;

namespace DotNetScaffoldStudio.Infrastructure;

public sealed class GitStatusService(ICommandRunner runner, string gitExecutable = "git") : IGitStatusService
{
    public async Task<GitWorkspaceState> GetStatusAsync(string workspaceRoot, CancellationToken cancellationToken)
    {
        CommandResult repositoryCheck;
        try
        {
            repositoryCheck = await RunAsync(workspaceRoot, ["rev-parse", "--is-inside-work-tree"], cancellationToken);
        }
        catch (Win32Exception)
        {
            return new GitWorkspaceState(false, null, []);
        }

        if (!repositoryCheck.Succeeded ||
            !repositoryCheck.StandardOutput.Any(line => string.Equals(line.Trim(), "true", StringComparison.OrdinalIgnoreCase)))
        {
            return new GitWorkspaceState(false, null, []);
        }

        var branchResult = await RunAsync(workspaceRoot, ["branch", "--show-current"], cancellationToken);
        var statusResult = await RunAsync(workspaceRoot, ["status", "--porcelain=v1", "--untracked-files=all"], cancellationToken);
        var branch = branchResult.Succeeded ? branchResult.StandardOutput.FirstOrDefault()?.Trim() : null;
        var changes = statusResult.Succeeded ? ParsePorcelain(statusResult.StandardOutput) : [];
        return new GitWorkspaceState(true, string.IsNullOrWhiteSpace(branch) ? null : branch, changes);
    }

    public static IReadOnlyList<GitFileChange> ParsePorcelain(IEnumerable<string> lines) => lines
        .Where(line => line.Length >= 3)
        .Select(line => new GitFileChange(line[..2], line[3..].Trim('"')))
        .ToArray();

    private Task<CommandResult> RunAsync(string workspaceRoot, IReadOnlyList<string> arguments, CancellationToken cancellationToken) =>
        runner.RunAsync(
            new CommandRequest(
                gitExecutable,
                arguments.Select(argument => new CommandArgument(argument)).ToArray(),
                workspaceRoot,
                FeatureRisk.Normal,
                modifiesWorkspace: false,
                environment: new Dictionary<string, string> { ["LC_ALL"] = "C" }),
            null,
            cancellationToken);
}
