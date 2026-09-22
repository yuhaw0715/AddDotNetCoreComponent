using DotNetScaffoldStudio.Domain;

namespace DotNetScaffoldStudio.Application;

public interface IWorkspaceService
{
    Task<IReadOnlyList<ProjectInfo>> ScanProjectsAsync(string path, CancellationToken cancellationToken);
}

public interface IDemoExecutionService
{
    Task<DemoExecutionResult> ExecuteAsync(
        CommandPreview command,
        IProgress<string> progress,
        CancellationToken cancellationToken);
}

public interface ICommandRunner
{
    Task<CommandResult> RunAsync(
        CommandRequest request,
        IProgress<CommandOutputLine>? progress,
        CancellationToken cancellationToken);
}

public interface ICommandExecutionWorkflow
{
    Task<CommandExecutionResult> ExecuteAsync(
        string workspaceRoot,
        CommandRequest request,
        IProgress<CommandOutputLine>? progress,
        CancellationToken cancellationToken);
}

public interface IDotNetEnvironmentDiscovery
{
    Task<DotNetEnvironmentSnapshot> DiscoverAsync(string workspaceRoot, CancellationToken cancellationToken);
}

public interface IDotNetTemplateDiscovery
{
    Task<DotNetTemplateDiscoveryResult> DiscoverAsync(string workspaceRoot, CancellationToken cancellationToken);
}

public interface ICustomTemplateHelpDiscovery
{
    Task<CustomTemplateHelpDiscoveryResult> DiscoverAsync(
        string workspaceRoot,
        string templateShortName,
        CancellationToken cancellationToken);
}

public interface ITargetProjectValidator
{
    TargetValidationResult Validate(string workspaceRoot, string projectPath, string commandWorkingDirectory);
}

public interface IGitStatusService
{
    Task<GitWorkspaceState> GetStatusAsync(string workspaceRoot, CancellationToken cancellationToken);
}

public interface IFileSnapshotService
{
    Task<FileSnapshot> CaptureAsync(string workspaceRoot, CancellationToken cancellationToken);
    IReadOnlyList<FileChange> Compare(FileSnapshot before, FileSnapshot after);
}
