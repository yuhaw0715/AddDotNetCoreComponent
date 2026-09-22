using DotNetScaffoldStudio.Application;
using DotNetScaffoldStudio.Domain;

namespace DotNetScaffoldStudio.Infrastructure;

public sealed class LocalToolInstallationService(
    IDependencyDiscovery dependencyDiscovery,
    string dotNetExecutable = "dotnet") : ILocalToolInstallationService
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
        IReadOnlyList<DependencyRequirement> toolRequirements,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentNullException.ThrowIfNull(toolRequirements);
        ArgumentException.ThrowIfNullOrWhiteSpace(dotNetExecutable);

        var root = Path.GetFullPath(workspaceRoot);
        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException($"找不到工作區：{root}");
        }

        var requirements = toolRequirements.ToArray();
        if (requirements.Any(requirement => requirement.Kind != DependencyKind.LocalTool))
        {
            throw new ArgumentException("本機工具安裝計畫只能包含 LocalTool 相依性。", nameof(toolRequirements));
        }

        var manifestRequirement = new DependencyRequirement(
            DependencyKind.ToolManifest,
            ".config/dotnet-tools.json");
        var discovery = await dependencyDiscovery.DiscoverAsync(
            root,
            null,
            [manifestRequirement, .. requirements],
            cancellationToken);
        var manifest = Find(discovery, manifestRequirement);
        var steps = new List<DependencyPlanStep>();
        var diagnostics = discovery.Diagnostics.ToList();

        if (manifest.Status == EnvironmentDetectionStatus.Missing)
        {
            steps.Add(CreateManifestStep(manifest, root));
        }
        else if (manifest.Status == EnvironmentDetectionStatus.DetectionFailed)
        {
            diagnostics.Add("無法確認本機工具資訊清單狀態，未建立安裝命令。");
        }

        foreach (var requirement in requirements)
        {
            var capability = Find(discovery, requirement);
            switch (capability.Status)
            {
                case EnvironmentDetectionStatus.Missing:
                case EnvironmentDetectionStatus.Incompatible:
                    steps.Add(CreateToolStep(capability, root));
                    break;
                case EnvironmentDetectionStatus.DetectionFailed:
                    diagnostics.Add($"無法確認工具 {requirement.Id} 狀態，未建立安裝命令。");
                    break;
            }
        }

        return new DependencyPlan(root, null, discovery.Capabilities, steps, diagnostics);
    }

    private DependencyPlanStep CreateManifestStep(
        DependencyCapability capability,
        string workspaceRoot) =>
        new(
            capability,
            CreateRequest(
                workspaceRoot,
                [new("new"), new("tool-manifest")]),
            requiresNetwork: false,
            [Path.Combine(".config", "dotnet-tools.json")]);

    private DependencyPlanStep CreateToolStep(
        DependencyCapability capability,
        string workspaceRoot)
    {
        var requirement = capability.Requirement;
        var command = capability.Status == EnvironmentDetectionStatus.Incompatible
            ? "update"
            : "install";
        var arguments = new List<CommandArgument>
        {
            new("tool"),
            new(command),
            new(requirement.Id),
            new("--local")
        };
        if (!string.IsNullOrWhiteSpace(requirement.MinimumVersion))
        {
            arguments.Add(new("--version"));
            arguments.Add(new(requirement.MinimumVersion));
        }

        return new DependencyPlanStep(
            capability,
            CreateRequest(workspaceRoot, arguments),
            requiresNetwork: true,
            [Path.Combine(".config", "dotnet-tools.json")]);
    }

    private CommandRequest CreateRequest(
        string workspaceRoot,
        IReadOnlyList<CommandArgument> arguments) =>
        new(
            dotNetExecutable,
            arguments,
            workspaceRoot,
            FeatureRisk.EnvironmentChange,
            modifiesWorkspace: true,
            environment: CommandEnvironment);

    private static DependencyCapability Find(
        DependencyDiscoveryResult discovery,
        DependencyRequirement requirement) =>
        discovery.Capabilities.FirstOrDefault(capability =>
            capability.Requirement.Kind == requirement.Kind &&
            capability.Requirement.Id.Equals(requirement.Id, StringComparison.OrdinalIgnoreCase))
        ?? throw new InvalidOperationException($"相依性偵測結果缺少 {requirement.Kind}:{requirement.Id}。");
}
