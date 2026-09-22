namespace DotNetScaffoldStudio.Domain;

using System.Security.Cryptography;
using System.Text;

public sealed record DependencyCapability
{
    public DependencyCapability(
        DependencyRequirement requirement,
        EnvironmentDetectionStatus status,
        string? detectedVersion,
        string? source,
        string? reason)
    {
        ArgumentNullException.ThrowIfNull(requirement);

        Requirement = requirement;
        Status = status;
        DetectedVersion = string.IsNullOrWhiteSpace(detectedVersion) ? null : detectedVersion;
        Source = string.IsNullOrWhiteSpace(source) ? null : source;
        Reason = string.IsNullOrWhiteSpace(reason) ? null : reason;
    }

    public DependencyRequirement Requirement { get; }
    public EnvironmentDetectionStatus Status { get; }
    public string? DetectedVersion { get; }
    public string? Source { get; }
    public string? Reason { get; }
    public bool IsAvailable => Status == EnvironmentDetectionStatus.Available;
}

public sealed record DependencyDiscoveryResult
{
    public DependencyDiscoveryResult(
        EnvironmentDetectionStatus status,
        IReadOnlyList<DependencyCapability> capabilities,
        IReadOnlyList<string> diagnostics)
    {
        Status = status;
        Capabilities = Array.AsReadOnly(capabilities.ToArray());
        Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
    }

    public EnvironmentDetectionStatus Status { get; }
    public IReadOnlyList<DependencyCapability> Capabilities { get; }
    public IReadOnlyList<string> Diagnostics { get; }
    public bool IsFullyAvailable => Status == EnvironmentDetectionStatus.Available;
}

public sealed record DependencyPlanStep
{
    public DependencyPlanStep(
        DependencyCapability dependency,
        CommandRequest request,
        bool requiresNetwork,
        IReadOnlyList<string>? expectedChanges = null)
    {
        ArgumentNullException.ThrowIfNull(dependency);
        ArgumentNullException.ThrowIfNull(request);

        if (!request.ModifiesWorkspace)
        {
            throw new ArgumentException("相依性計畫步驟必須明確標示會修改工作區。", nameof(request));
        }

        Dependency = dependency;
        Request = request;
        RequiresNetwork = requiresNetwork;
        ExpectedChanges = Array.AsReadOnly(expectedChanges?.ToArray() ?? []);
    }

    public DependencyCapability Dependency { get; }
    public CommandRequest Request { get; }
    public bool RequiresNetwork { get; }
    public IReadOnlyList<string> ExpectedChanges { get; }
}

public sealed record DependencyPlan
{
    public DependencyPlan(
        string workspaceRoot,
        string? targetProjectPath,
        IReadOnlyList<DependencyCapability> capabilities,
        IReadOnlyList<DependencyPlanStep> steps,
        IReadOnlyList<string>? diagnostics = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentNullException.ThrowIfNull(capabilities);
        ArgumentNullException.ThrowIfNull(steps);

        WorkspaceRoot = Path.GetFullPath(workspaceRoot);
        TargetProjectPath = string.IsNullOrWhiteSpace(targetProjectPath)
            ? null
            : Path.GetFullPath(targetProjectPath);
        Capabilities = Array.AsReadOnly(capabilities.ToArray());
        Steps = Array.AsReadOnly(steps.ToArray());
        Diagnostics = Array.AsReadOnly(diagnostics?.ToArray() ?? []);

        if (Steps.Select(step => $"{step.Dependency.Requirement.Kind}:{step.Dependency.Requirement.Id}")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count() != Steps.Count)
        {
            throw new ArgumentException("相依性計畫不可包含重複的步驟。", nameof(steps));
        }

        if (Steps.Any(step => !string.Equals(
                step.Request.WorkingDirectory,
                WorkspaceRoot,
                StringComparison.Ordinal)))
        {
            throw new ArgumentException("相依性計畫的命令工作目錄必須是工作區根目錄。", nameof(steps));
        }
    }

    public string WorkspaceRoot { get; }
    public string? TargetProjectPath { get; }
    public IReadOnlyList<DependencyCapability> Capabilities { get; }
    public IReadOnlyList<DependencyPlanStep> Steps { get; }
    public IReadOnlyList<string> Diagnostics { get; }
    public bool RequiresConfirmation => Steps.Count > 0;

    public string GetConfirmationFingerprint()
    {
        var content = string.Join(
            '\u001f',
            [
                WorkspaceRoot,
                TargetProjectPath ?? string.Empty,
                .. Capabilities.Select(capability =>
                    $"{capability.Requirement.Kind}:{capability.Requirement.Id}:{capability.Requirement.MinimumVersion}:{capability.Status}:{capability.DetectedVersion}"),
                .. Steps.Select(step => string.Join(
                    '\u001e',
                    new[]
                    {
                        step.Dependency.Requirement.Kind.ToString(),
                        step.Dependency.Requirement.Id,
                        step.RequiresNetwork.ToString(),
                        step.Request.GetConfirmationFingerprint()
                    }.Concat(step.ExpectedChanges)))
            ]);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content)));
    }
}

public sealed record DependencyPlanConfirmation(Guid Id, string PlanFingerprint);

public enum DependencyPlanStepStatus
{
    Succeeded,
    Failed,
    RevalidationFailed,
    Skipped
}

public enum DependencyPlanExecutionStatus
{
    Succeeded,
    ConfirmationRejected,
    ExecutionFailed,
    PartiallySucceeded,
    RevalidationFailed
}
