using DotNetScaffoldStudio.Application;
using DotNetScaffoldStudio.Domain;
using DotNetScaffoldStudio.Infrastructure;

namespace DotNetScaffoldStudio.IntegrationTests;

public sealed class NuGetPackageInstallationServiceTests
{
    [Fact]
    public async Task ExecuteAsync_AddsPackageOnlyToSelectedProjectThenRestores()
    {
        using var workspace = TemporaryDirectory.Create();
        var projectPath = await CreateProjectAsync(workspace.Path, "src/Demo.Api/Demo.Api.csproj");
        var otherProjectPath = await CreateProjectAsync(workspace.Path, "src/Other/Other.csproj");
        var service = CreateService(workspace.Path);

        var plan = await service.CreatePlanAsync(
            workspace.Path,
            projectPath,
            [new DependencyRequirement(DependencyKind.NuGetPackage, "Contoso.Design", "1.2.3")],
            CancellationToken.None);

        Assert.Equal(projectPath, plan.TargetProjectPath);
        Assert.Equal(
            [DependencyPlanStepKind.NuGetPackageInstallation, DependencyPlanStepKind.NuGetRestore],
            plan.Steps.Select(step => step.Kind));
        Assert.Equal(
            ["add", "src/Demo.Api/Demo.Api.csproj", "package", "Contoso.Design", "--version", "1.2.3", "--no-restore"],
            Values(plan.Steps[0].Request));
        Assert.Equal(["restore", "src/Demo.Api/Demo.Api.csproj"], Values(plan.Steps[1].Request));
        Assert.All(plan.Steps, step =>
        {
            Assert.True(step.RequiresNetwork);
            Assert.Equal("1", step.Request.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"]);
        });

        var result = await ExecutePlanAsync(workspace.Path, plan);

        Assert.Equal(DependencyPlanExecutionStatus.Succeeded, result.Status);
        Assert.All(result.Steps, step => Assert.Equal(DependencyPlanStepStatus.Succeeded, step.Status));
        var projectText = await File.ReadAllTextAsync(projectPath);
        var otherProjectText = await File.ReadAllTextAsync(otherProjectPath);
        Assert.Contains("Include=\"Contoso.Design\"", projectText, StringComparison.Ordinal);
        Assert.Contains("Version=\"1.2.3\"", projectText, StringComparison.Ordinal);
        Assert.DoesNotContain("Contoso.Design", otherProjectText, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(workspace.Path, "src", "Demo.Api", "obj", "project.assets.json")));
        Assert.Contains(
            new FileChange(FileChangeKind.Modified, "src/Demo.Api/Demo.Api.csproj"),
            result.Differences!.CommandFileChanges);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRestoreFailsReportsRestoreStageAndKeepsProjectChange()
    {
        using var workspace = TemporaryDirectory.Create();
        var projectPath = await CreateProjectAsync(workspace.Path, "Demo.Api.csproj");
        var service = CreateService(
            workspace.Path,
            new Dictionary<string, string>
            {
                ["DOTNET_SCAFFOLD_STUDIO_FIXTURE_RESTORE_FAILURE"] = "1"
            });
        var plan = await service.CreatePlanAsync(
            workspace.Path,
            projectPath,
            [new DependencyRequirement(DependencyKind.NuGetPackage, "Contoso.Design", "1.2.3")],
            CancellationToken.None);

        var result = await ExecutePlanAsync(workspace.Path, plan);

        Assert.Equal(DependencyPlanExecutionStatus.PartiallySucceeded, result.Status);
        Assert.Equal(
            [DependencyPlanStepStatus.Succeeded, DependencyPlanStepStatus.Failed],
            result.Steps.Select(step => step.Status));
        Assert.Contains("NuGet 還原", result.Diagnostics.Single(), StringComparison.Ordinal);
        Assert.Contains("fixture restore failed", result.Diagnostics.Single(), StringComparison.Ordinal);
        Assert.Contains("未自動回復", result.Diagnostics.Single(), StringComparison.Ordinal);
        var projectText = await File.ReadAllTextAsync(projectPath);
        Assert.Contains("Include=\"Contoso.Design\"", projectText, StringComparison.Ordinal);
        Assert.Contains(
            new FileChange(FileChangeKind.Modified, "Demo.Api.csproj"),
            result.Differences!.CommandFileChanges);
    }

    private static NuGetPackageInstallationService CreateService(
        string workspaceRoot,
        IReadOnlyDictionary<string, string>? additionalEnvironment = null)
    {
        var probeName = OperatingSystem.IsWindows()
            ? "DotNetScaffoldStudio.CommandProbe.exe"
            : "DotNetScaffoldStudio.CommandProbe";
        var probePath = Path.Combine(AppContext.BaseDirectory, probeName);
        Assert.True(File.Exists(probePath), $"找不到命令 fixture：{probePath}");
        var environment = new Dictionary<string, string>(additionalEnvironment ?? new Dictionary<string, string>(), StringComparer.Ordinal)
        {
            ["DOTNET_CLI_HOME"] = Path.Combine(workspaceRoot, ".dotnet-cli-home"),
            ["NUGET_PACKAGES"] = Path.Combine(workspaceRoot, ".nuget-packages")
        };
        return new NuGetPackageInstallationService(
            new DependencyDiscoveryService(new FixtureEnvironmentDiscovery()),
            probePath,
            environment);
    }

    private static async Task<DependencyPlanExecutionResult> ExecutePlanAsync(
        string workspaceRoot,
        DependencyPlan plan)
    {
        var discovery = new DependencyDiscoveryService(new FixtureEnvironmentDiscovery());
        var execution = new CommandExecutionWorkflow(
            new CommandCoordinator(new ProcessCommandRunner()),
            new FixtureGitStatusService(),
            new FileSnapshotService());
        var confirmationPolicy = new DependencyPlanConfirmationPolicy();
        return await new DependencyPlanWorkflow(execution, discovery, confirmationPolicy)
            .ExecuteAsync(plan, confirmationPolicy.Issue(plan), null, CancellationToken.None);
    }

    private static IReadOnlyList<string> Values(CommandRequest request) =>
        request.Arguments.Select(argument => argument.Value).ToArray();

    private static async Task<string> CreateProjectAsync(string root, string relativePath)
    {
        var path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(
            path,
            "<Project Sdk=\"Microsoft.NET.Sdk.Web\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
        return path;
    }

    private sealed class FixtureEnvironmentDiscovery : IDotNetEnvironmentDiscovery
    {
        public Task<DotNetEnvironmentSnapshot> DiscoverAsync(
            string workspaceRoot,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(
                new DotNetEnvironmentSnapshot(
                    EnvironmentDetectionStatus.Available,
                    "10.0.10",
                    "arm64",
                    "10.0.100",
                    EnvironmentDetectionStatus.Available,
                    [new DotNetSdkInstallation("10.0.100", "/fixture/sdk")],
                    EnvironmentDetectionStatus.Available,
                    [new DotNetRuntimeInstallation("Microsoft.NETCore.App", "10.0.10", "/fixture/shared")],
                    EnvironmentDetectionStatus.Available,
                    [],
                    EnvironmentDetectionStatus.Missing,
                    EnvironmentDetectionStatus.Missing,
                    [],
                    []));
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
                $"dotnet-scaffold-studio-nuget-{Guid.NewGuid():N}");
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
