using System.ComponentModel;
using DotNetScaffoldStudio.Application;
using DotNetScaffoldStudio.Domain;
using DotNetScaffoldStudio.Infrastructure;

namespace DotNetScaffoldStudio.IntegrationTests;

public sealed class DotNetEnvironmentDiscoveryServiceTests
{
    [Fact]
    public async Task DiscoverAsync_WhenDotNetExecutableIsMissing_ReportsMissingAndStopsProbing()
    {
        using var workspace = TemporaryDirectory.Create();
        var runner = new FixtureRunner(_ => throw new Win32Exception(2, "file not found"));

        var result = await new DotNetEnvironmentDiscoveryService(runner, "missing-dotnet")
            .DiscoverAsync(workspace.Path, CancellationToken.None);

        Assert.Equal(EnvironmentDetectionStatus.Missing, result.DotNetStatus);
        Assert.Equal(EnvironmentDetectionStatus.DetectionFailed, result.SdkStatus);
        Assert.Single(runner.Commands);
        Assert.Contains("dotnet --info", result.Diagnostics[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task DiscoverAsync_WhenOnlyRuntimeIsInstalled_DisablesSdkAvailability()
    {
        using var workspace = TemporaryDirectory.Create();
        var runner = new FixtureRunner(request => request.Arguments[0].Value switch
        {
            "--info" => Result(0, ["Host:", "  Version: 10.0.10", ".NET SDKs installed:"]),
            "--list-sdks" => Result(0, []),
            "--list-runtimes" => Result(0, [
                "Microsoft.AspNetCore.App 10.0.10 [/dotnet/shared/Microsoft.AspNetCore.App]",
                "Microsoft.NETCore.App 10.0.10 [/dotnet/shared/Microsoft.NETCore.App]"]),
            "workload" => Result(0, ["{\"installed\":[],\"updateAvailable\":[]}"]),
            _ => throw new InvalidOperationException("Unexpected fixture command.")
        });

        var result = await new DotNetEnvironmentDiscoveryService(runner)
            .DiscoverAsync(workspace.Path, CancellationToken.None);

        Assert.Equal(EnvironmentDetectionStatus.Available, result.DotNetStatus);
        Assert.Equal("10.0.10", result.HostVersion);
        Assert.Null(result.ActiveSdkVersion);
        Assert.Equal(EnvironmentDetectionStatus.Missing, result.SdkStatus);
        Assert.Equal(EnvironmentDetectionStatus.Available, result.RuntimeStatus);
        Assert.Equal(2, result.Runtimes.Count);
        Assert.Equal(EnvironmentDetectionStatus.DetectionFailed, result.WorkloadStatus);
        Assert.Equal(EnvironmentDetectionStatus.Missing, result.ToolManifestStatus);
        Assert.Equal(EnvironmentDetectionStatus.Missing, result.LocalToolStatus);
        Assert.DoesNotContain(runner.Commands, request => request.Arguments[0].Value is "workload" or "tool");
    }

    [Fact]
    public async Task DiscoverAsync_ParsesMultipleSdksWorkloadsAndLocalTools()
    {
        using var workspace = TemporaryDirectory.Create();
        Directory.CreateDirectory(Path.Combine(workspace.Path, ".config"));
        await File.WriteAllTextAsync(
            Path.Combine(workspace.Path, ".config", "dotnet-tools.json"),
            "{\"version\": 1, \"isRoot\": true, \"tools\": {}} ");
        var runner = new FixtureRunner(request => request.Arguments[0].Value switch
        {
            "--info" => Result(0, [".NET SDK:", " Version:           10.0.100", " Commit:             fixture"]),
            "--list-sdks" => Result(0, ["10.0.100-preview.7.1 [/Applications/Dot Net/sdk]", "9.0.203 [/dotnet/sdk]"]),
            "--list-runtimes" => Result(0, ["Microsoft.NETCore.App 10.0.10 [/dotnet/shared/Microsoft.NETCore.App]"]),
            "workload" => Result(0, [
                "==workloadListJsonOutputStart==",
                "{\"installed\":[\"wasm-tools\"],\"updateAvailable\":[{\"workloadId\":\"wasm-tools\",\"existingManifestVersion\":\"10.0.100/10.0.100\",\"availableUpdateManifestVersion\":\"10.0.101/10.0.101\"}]}",
                "==workloadListJsonOutputEnd=="]),
            "tool" => Result(0, [
                "Package Id                         Version      Commands       Manifest",
                "-------------------------------------------------------------------------------",
                "dotnet-ef                          10.0.0       dotnet-ef      /workspace/.config/dotnet-tools.json",
                "dotnet-aspnet-codegenerator       10.0.0       dotnet-aspnet-codegenerator /workspace/.config/dotnet-tools.json"]),
            _ => throw new InvalidOperationException("Unexpected fixture command.")
        });

        var result = await new DotNetEnvironmentDiscoveryService(runner)
            .DiscoverAsync(workspace.Path, CancellationToken.None);

        Assert.Equal("10.0.100", result.ActiveSdkVersion);
        Assert.Equal(EnvironmentDetectionStatus.Available, result.SdkStatus);
        Assert.Equal(["10.0.100-preview.7.1", "9.0.203"], result.Sdks.Select(sdk => sdk.Version));
        Assert.Equal("/Applications/Dot Net/sdk", result.Sdks[0].BasePath);
        Assert.Equal(EnvironmentDetectionStatus.Available, result.WorkloadStatus);
        var workload = Assert.Single(result.Workloads);
        Assert.Equal("wasm-tools", workload.Id);
        Assert.Equal("10.0.100/10.0.100", workload.InstalledManifestVersion);
        Assert.Equal(EnvironmentDetectionStatus.Available, result.ToolManifestStatus);
        Assert.Equal(EnvironmentDetectionStatus.Available, result.LocalToolStatus);
        Assert.Contains(result.LocalTools, tool => tool.PackageId == "dotnet-ef" && tool.Version == "10.0.0");
        Assert.Contains(result.LocalTools, tool => tool.PackageId == "dotnet-aspnet-codegenerator");
        Assert.Contains(runner.Commands, request => request.Arguments.Select(argument => argument.Value)
            .SequenceEqual(["workload", "list", "--machine-readable"]));
        Assert.Contains(runner.Commands, request => request.Arguments.Select(argument => argument.Value)
            .SequenceEqual(["tool", "list", "--local"]));
        Assert.DoesNotContain(runner.Commands, request => request.Arguments.Any(argument => argument.Value == "--global"));
        Assert.All(runner.Commands, request =>
        {
            Assert.Equal("dotnet", request.Executable);
            Assert.Equal(workspace.Path, request.WorkingDirectory);
            Assert.False(request.ModifiesWorkspace);
            Assert.Equal("C", request.Environment["LC_ALL"]);
            Assert.Equal("en", request.Environment["DOTNET_CLI_UI_LANGUAGE"]);
            Assert.Equal("1", request.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"]);
            Assert.Equal("true", request.Environment["DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE"]);
            Assert.Equal("1", request.Environment["DOTNET_NOLOGO"]);
        });
    }

    [Fact]
    public async Task DiscoverAsync_WhenWorkloadListFails_PreservesOtherSuccessfulStages()
    {
        using var workspace = TemporaryDirectory.Create();
        var runner = new FixtureRunner(request => request.Arguments[0].Value switch
        {
            "--info" => Result(0, [".NET SDK:", " Version: 10.0.100"]),
            "--list-sdks" => Result(0, ["10.0.100 [/dotnet/sdk]"]),
            "--list-runtimes" => Result(0, ["Microsoft.NETCore.App 10.0.10 [/dotnet/shared/Microsoft.NETCore.App]"]),
            "workload" => Result(1, [], ["workload list failed"]),
            _ => throw new InvalidOperationException("Unexpected fixture command.")
        });

        var result = await new DotNetEnvironmentDiscoveryService(runner)
            .DiscoverAsync(workspace.Path, CancellationToken.None);

        Assert.Equal(EnvironmentDetectionStatus.Available, result.SdkStatus);
        Assert.Equal(EnvironmentDetectionStatus.Available, result.RuntimeStatus);
        Assert.Equal(EnvironmentDetectionStatus.DetectionFailed, result.WorkloadStatus);
        Assert.Contains("workload list failed", result.Diagnostics[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task DiscoverAsync_WhenInfoCommandFails_ReportsDetectionFailure()
    {
        using var workspace = TemporaryDirectory.Create();
        var runner = new FixtureRunner(_ => Result(1, [], ["dotnet info failed"]));

        var result = await new DotNetEnvironmentDiscoveryService(runner)
            .DiscoverAsync(workspace.Path, CancellationToken.None);

        Assert.Equal(EnvironmentDetectionStatus.DetectionFailed, result.DotNetStatus);
        Assert.All(
            [result.SdkStatus, result.RuntimeStatus, result.WorkloadStatus],
            status => Assert.Equal(EnvironmentDetectionStatus.DetectionFailed, status));
        Assert.Contains("dotnet info failed", result.Diagnostics[0], StringComparison.Ordinal);
        Assert.Single(runner.Commands);
    }

    private static CommandResult Result(
        int exitCode,
        IReadOnlyList<string> stdout,
        IReadOnlyList<string>? stderr = null)
    {
        var now = DateTimeOffset.UtcNow;
        return new CommandResult(exitCode, now, now, stdout, stderr ?? []);
    }

    private sealed class FixtureRunner(Func<CommandRequest, CommandResult> respond) : ICommandRunner
    {
        public List<CommandRequest> Commands { get; } = [];

        public Task<CommandResult> RunAsync(
            CommandRequest request,
            IProgress<CommandOutputLine>? progress,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Commands.Add(request);
            return Task.FromResult(respond(request));
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        private TemporaryDirectory(string path) => Path = path;

        public string Path { get; }

        public static TemporaryDirectory Create()
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"dotnet-scaffold-studio-environment-{Guid.NewGuid():N}");
            Directory.CreateDirectory(path);
            return new TemporaryDirectory(path);
        }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
