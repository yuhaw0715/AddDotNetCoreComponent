using DotNetScaffoldStudio.Application;
using DotNetScaffoldStudio.Domain;

namespace DotNetScaffoldStudio.Infrastructure;

public sealed class NuGetPackageInstallationService(
    IDependencyDiscovery dependencyDiscovery,
    string dotNetExecutable = "dotnet",
    IReadOnlyDictionary<string, string>? additionalEnvironment = null) : INuGetPackageInstallationService
{
    private static readonly IReadOnlyDictionary<string, string> CommandEnvironment =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1",
            ["DOTNET_CLI_UI_LANGUAGE"] = "en",
            ["DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE"] = "true",
            ["DOTNET_NOLOGO"] = "1",
            ["LC_ALL"] = "C"
        };

    public async Task<DependencyPlan> CreatePlanAsync(
        string workspaceRoot,
        string targetProjectPath,
        IReadOnlyList<DependencyRequirement> packageRequirements,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetProjectPath);
        ArgumentNullException.ThrowIfNull(packageRequirements);
        ArgumentException.ThrowIfNullOrWhiteSpace(dotNetExecutable);

        var root = Path.GetFullPath(workspaceRoot);
        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException($"找不到工作區：{root}");
        }

        var projectPath = Path.IsPathFullyQualified(targetProjectPath)
            ? Path.GetFullPath(targetProjectPath)
            : Path.GetFullPath(Path.Combine(root, targetProjectPath));
        ValidateTargetProject(root, projectPath);

        var requirements = packageRequirements.ToArray();
        if (requirements.Any(requirement => requirement.Kind != DependencyKind.NuGetPackage))
        {
            throw new ArgumentException("NuGet 安裝計畫只能包含 NuGetPackage 相依性。", nameof(packageRequirements));
        }

        var discovery = await dependencyDiscovery.DiscoverAsync(
            root,
            projectPath,
            requirements,
            cancellationToken);
        var steps = new List<DependencyPlanStep>();
        var diagnostics = discovery.Diagnostics.ToList();
        var projectRelativePath = Path.GetRelativePath(root, projectPath);
        var installCapabilities = new List<DependencyCapability>();
        foreach (var requirement in requirements)
        {
            var capability = Find(discovery, requirement);
            if (capability.Status is EnvironmentDetectionStatus.Missing or EnvironmentDetectionStatus.Incompatible)
            {
                installCapabilities.Add(capability);
                steps.Add(CreateInstallStep(capability, root, projectRelativePath));
            }
            else if (capability.Status == EnvironmentDetectionStatus.DetectionFailed)
            {
                diagnostics.Add($"無法確認 NuGet 套件 {requirement.Id} 狀態，未建立安裝命令。");
            }
        }

        if (installCapabilities.Count > 0)
        {
            steps.Add(CreateRestoreStep(installCapabilities[0], root, projectRelativePath));
        }

        return new DependencyPlan(root, projectPath, discovery.Capabilities, steps, diagnostics);
    }

    private DependencyPlanStep CreateInstallStep(
        DependencyCapability capability,
        string workspaceRoot,
        string projectRelativePath)
    {
        var arguments = new List<CommandArgument>
        {
            new("add"),
            new(projectRelativePath),
            new("package"),
            new(capability.Requirement.Id)
        };
        if (!string.IsNullOrWhiteSpace(capability.Requirement.MinimumVersion))
        {
            arguments.Add(new("--version"));
            arguments.Add(new(capability.Requirement.MinimumVersion));
        }

        arguments.Add(new("--no-restore"));
        return new DependencyPlanStep(
            capability,
            CreateRequest(workspaceRoot, arguments),
            requiresNetwork: true,
            expectedChanges: [projectRelativePath],
            kind: DependencyPlanStepKind.NuGetPackageInstallation);
    }

    private DependencyPlanStep CreateRestoreStep(
        DependencyCapability capability,
        string workspaceRoot,
        string projectRelativePath) =>
        new(
            capability,
            CreateRequest(workspaceRoot, [new("restore"), new(projectRelativePath)]),
            requiresNetwork: true,
            expectedChanges:
            [
                Path.Combine(Path.GetDirectoryName(projectRelativePath) ?? string.Empty, "obj", "project.assets.json")
            ],
            kind: DependencyPlanStepKind.NuGetRestore);

    private CommandRequest CreateRequest(
        string workspaceRoot,
        IReadOnlyList<CommandArgument> arguments)
    {
        var environment = new Dictionary<string, string>(
            additionalEnvironment ?? new Dictionary<string, string>(),
            StringComparer.Ordinal);
        foreach (var pair in CommandEnvironment)
        {
            environment[pair.Key] = pair.Value;
        }

        return new CommandRequest(
            dotNetExecutable,
            arguments,
            workspaceRoot,
            FeatureRisk.EnvironmentChange,
            modifiesWorkspace: true,
            environment: environment);
    }

    private static void ValidateTargetProject(string workspaceRoot, string projectPath)
    {
        if (!string.Equals(Path.GetExtension(projectPath), ".csproj", StringComparison.OrdinalIgnoreCase) ||
            !File.Exists(projectPath))
        {
            throw new FileNotFoundException("找不到選定的 .csproj。", projectPath);
        }

        if (!WorkspacePathResolver.IsPathWithin(workspaceRoot, projectPath))
        {
            throw new InvalidOperationException("目標 .csproj 不在目前工作區內。");
        }
    }

    private static DependencyCapability Find(
        DependencyDiscoveryResult discovery,
        DependencyRequirement requirement) =>
        discovery.Capabilities.FirstOrDefault(capability =>
            capability.Requirement.Kind == requirement.Kind &&
            capability.Requirement.Id.Equals(requirement.Id, StringComparison.OrdinalIgnoreCase))
        ?? throw new InvalidOperationException($"相依性偵測結果缺少 {requirement.Kind}:{requirement.Id}。");
}
