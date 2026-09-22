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

public interface IDependencyDiscovery
{
    Task<DependencyDiscoveryResult> DiscoverAsync(
        string workspaceRoot,
        string? targetProjectPath,
        IReadOnlyList<DependencyRequirement> requirements,
        CancellationToken cancellationToken);
}

public interface IDependencyPlanWorkflow
{
    Task<DependencyPlanExecutionResult> ExecuteAsync(
        DependencyPlan plan,
        DependencyPlanConfirmation? confirmation,
        IProgress<CommandOutputLine>? progress,
        CancellationToken cancellationToken);
}

public interface ILocalToolInstallationService
{
    Task<DependencyPlan> CreatePlanAsync(
        string workspaceRoot,
        IReadOnlyList<DependencyRequirement> toolRequirements,
        CancellationToken cancellationToken);
}

public interface INuGetPackageInstallationService
{
    Task<DependencyPlan> CreatePlanAsync(
        string workspaceRoot,
        string targetProjectPath,
        IReadOnlyList<DependencyRequirement> packageRequirements,
        CancellationToken cancellationToken);
}

public interface IDependencyGuidanceService
{
    DependencyGuidanceResult Build(DependencyDiscoveryResult discovery);
}

public interface IWorkloadInstallationAdapter
{
    Task<WorkloadInstallationAdapterResult> InstallAsync(
        DependencyRequirement requirement,
        CancellationToken cancellationToken);
}

public interface IWorkloadInstallationWorkflow
{
    WorkloadInstallationPlan CreatePlan(DependencyGuidance guidance);

    Task<WorkloadInstallationExecutionResult> ExecuteAsync(
        WorkloadInstallationPlan plan,
        WorkloadInstallationConfirmation? confirmation,
        CancellationToken cancellationToken);
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
