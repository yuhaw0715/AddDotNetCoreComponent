using System.Text.Json;
using DotNetScaffoldStudio.Application;
using DotNetScaffoldStudio.Domain;
using DotNetScaffoldStudio.Infrastructure;

namespace DotNetScaffoldStudio.IntegrationTests;

public sealed class LocalToolInstallationServiceTests
{
    [Fact]
    public async Task CreatePlanAndExecuteAsync_UsesLocalManifestAndReportsToolFiles()
    {
        using var workspace = TemporaryDirectory.Create();
        var probeName = OperatingSystem.IsWindows()
            ? "DotNetScaffoldStudio.CommandProbe.exe"
            : "DotNetScaffoldStudio.CommandProbe";
        var probePath = Path.Combine(AppContext.BaseDirectory, probeName);
        Assert.True(File.Exists(probePath), $"找不到命令 fixture：{probePath}");

        var discovery = new FixtureDependencyDiscovery(workspace.Path);
        var installer = new LocalToolInstallationService(discovery, probePath);
        var plan = await installer.CreatePlanAsync(
            workspace.Path,
            [new DependencyRequirement(DependencyKind.LocalTool, "Contoso.Tool", "1.2.3")],
            CancellationToken.None);

        Assert.Equal(2, plan.Steps.Count);
        var manifestStep = plan.Steps[0];
        var toolStep = plan.Steps[1];
        Assert.Equal(["new", "tool-manifest"], Values(manifestStep.Request));
        Assert.Equal(
            ["tool", "install", "Contoso.Tool", "--local", "--version", "1.2.3"],
            Values(toolStep.Request));
        Assert.False(manifestStep.RequiresNetwork);
        Assert.True(toolStep.RequiresNetwork);
        Assert.All(plan.Steps, step =>
        {
            Assert.DoesNotContain(step.Request.Arguments, argument =>
                argument.Value.Equals("--global", StringComparison.OrdinalIgnoreCase));
            Assert.Equal("1", step.Request.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"]);
            Assert.Equal("en", step.Request.Environment["DOTNET_CLI_UI_LANGUAGE"]);
            Assert.Equal("true", step.Request.Environment["DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE"]);
        });

        var executionWorkflow = new CommandExecutionWorkflow(
            new CommandCoordinator(new ProcessCommandRunner()),
            new FixtureGitStatusService(),
            new FileSnapshotService());
        var confirmationPolicy = new DependencyPlanConfirmationPolicy();
        var result = await new DependencyPlanWorkflow(
                executionWorkflow,
                discovery,
                confirmationPolicy)
            .ExecuteAsync(
                plan,
                confirmationPolicy.Issue(plan),
                null,
                CancellationToken.None);

        Assert.Equal(DependencyPlanExecutionStatus.Succeeded, result.Status);
        Assert.All(result.Steps, step => Assert.Equal(DependencyPlanStepStatus.Succeeded, step.Status));

        var manifestPath = Path.Combine(workspace.Path, ".config", "dotnet-tools.json");
        var executablePath = Path.Combine(workspace.Path, ".config", "dotnet-tools", "Contoso.Tool");
        Assert.True(File.Exists(manifestPath));
        Assert.True(File.Exists(executablePath));
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(manifestPath));
        Assert.Equal(
            "1.2.3",
            document.RootElement.GetProperty("tools").GetProperty("Contoso.Tool").GetProperty("version").GetString());
        if (!OperatingSystem.IsWindows())
        {
            Assert.True(File.GetUnixFileMode(executablePath).HasFlag(UnixFileMode.UserExecute));
        }

        var changes = result.Differences!.CommandFileChanges;
        Assert.Contains(new FileChange(FileChangeKind.Added, ".config/dotnet-tools.json"), changes);
        Assert.Contains(new FileChange(FileChangeKind.Added, ".config/dotnet-tools/Contoso.Tool"), changes);
    }

    private static IReadOnlyList<string> Values(CommandRequest request) =>
        request.Arguments.Select(argument => argument.Value).ToArray();

    private sealed class FixtureDependencyDiscovery(string workspaceRoot) : IDependencyDiscovery
    {
        private bool _returnInitialMissing = true;

        public Task<DependencyDiscoveryResult> DiscoverAsync(
            string requestedWorkspaceRoot,
            string? targetProjectPath,
            IReadOnlyList<DependencyRequirement> requirements,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.Equal(workspaceRoot, requestedWorkspaceRoot);

            var capabilities = _returnInitialMissing
                ? requirements.Select(requirement => new DependencyCapability(
                    requirement,
                    EnvironmentDetectionStatus.Missing,
                    null,
                    Path.Combine(workspaceRoot, ".config", "dotnet-tools.json"),
                    "fixture 初始狀態為缺少。"))
                : requirements.Select(CreateCapability);
            _returnInitialMissing = false;
            var result = capabilities.ToArray();
            var status = result.Any(capability => capability.Status == EnvironmentDetectionStatus.DetectionFailed)
                ? EnvironmentDetectionStatus.DetectionFailed
                : result.Any(capability => capability.Status == EnvironmentDetectionStatus.Incompatible)
                    ? EnvironmentDetectionStatus.Incompatible
                    : result.Any(capability => capability.Status == EnvironmentDetectionStatus.Missing)
                        ? EnvironmentDetectionStatus.Missing
                        : EnvironmentDetectionStatus.Available;
            return Task.FromResult(new DependencyDiscoveryResult(status, result, []));
        }

        private DependencyCapability CreateCapability(DependencyRequirement requirement)
        {
            var manifestPath = Path.Combine(workspaceRoot, ".config", "dotnet-tools.json");
            if (requirement.Kind == DependencyKind.ToolManifest)
            {
                return new DependencyCapability(
                    requirement,
                    File.Exists(manifestPath)
                        ? EnvironmentDetectionStatus.Available
                        : EnvironmentDetectionStatus.Missing,
                    null,
                    manifestPath,
                    File.Exists(manifestPath) ? null : "fixture 找不到本機工具資訊清單。");
            }

            var version = ReadToolVersion(manifestPath, requirement.Id);
            return version is null
                ? new DependencyCapability(
                    requirement,
                    EnvironmentDetectionStatus.Missing,
                    null,
                    manifestPath,
                    "fixture 找不到本機工具。")
                : new DependencyCapability(
                    requirement,
                    EnvironmentDetectionStatus.Available,
                    version,
                    manifestPath,
                    null);
        }

        private static string? ReadToolVersion(string manifestPath, string packageId)
        {
            if (!File.Exists(manifestPath))
            {
                return null;
            }

            using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
            return document.RootElement.GetProperty("tools").TryGetProperty(packageId, out var tool) &&
                   tool.TryGetProperty("version", out var version)
                ? version.GetString()
                : null;
        }
    }

    private sealed class FixtureGitStatusService : IGitStatusService
    {
        public Task<GitWorkspaceState> GetStatusAsync(string workspaceRoot, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new GitWorkspaceState(false, null, []));
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        private TemporaryDirectory(string path) => Path = path;

        public string Path { get; }

        public static TemporaryDirectory Create()
        {
            var path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"dotnet-scaffold-studio-local-tools-{Guid.NewGuid():N}");
            Directory.CreateDirectory(path);
            return new TemporaryDirectory(path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
