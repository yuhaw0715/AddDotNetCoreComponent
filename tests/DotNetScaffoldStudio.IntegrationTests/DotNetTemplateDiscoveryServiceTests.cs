using DotNetScaffoldStudio.Application;
using DotNetScaffoldStudio.Domain;
using DotNetScaffoldStudio.Infrastructure;

namespace DotNetScaffoldStudio.IntegrationTests;

public sealed class DotNetTemplateDiscoveryServiceTests
{
    [Fact]
    public async Task DiscoverAsync_UsesFixedEnglishCliAndPreservesRawOutput()
    {
        using var workspace = TemporaryDirectory.Create();
        string[] stdout = [
            "Template Name             Short Name    Language  Tags",
            "------------------------  ------------  --------  ----------------",
            "Console App               console       [C#]      Common/Console"
        ];
        var runner = new FixtureRunner(_ => Result(0, stdout, ["template cache warning"]));

        var result = await new DotNetTemplateDiscoveryService(runner)
            .DiscoverAsync(workspace.Path, CancellationToken.None);

        var request = Assert.Single(runner.Commands);
        Assert.Equal("dotnet", request.Executable);
        Assert.Equal(["new", "list"], request.Arguments.Select(argument => argument.Value));
        Assert.False(request.ModifiesWorkspace);
        Assert.Equal(FeatureRisk.Normal, request.Risk);
        Assert.Equal("en", request.Environment["DOTNET_CLI_UI_LANGUAGE"]);
        Assert.Equal("C", request.Environment["LC_ALL"]);
        Assert.Equal("1", request.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"]);
        Assert.Equal(stdout, result.RawStandardOutput);
        Assert.Equal(["template cache warning"], result.RawStandardError);
        Assert.Equal(EnvironmentDetectionStatus.Available, result.Status);
        Assert.Contains(result.Templates, template => template.ShortNames.Contains("console"));
    }

    [Fact]
    public async Task DiscoverAsync_WhenListCommandFails_PreservesFailureOutput()
    {
        using var workspace = TemporaryDirectory.Create();
        var runner = new FixtureRunner(_ => Result(2, ["partial output"], ["list failed"]));

        var result = await new DotNetTemplateDiscoveryService(runner)
            .DiscoverAsync(workspace.Path, CancellationToken.None);

        Assert.Equal(EnvironmentDetectionStatus.DetectionFailed, result.Status);
        Assert.Equal(["partial output"], result.RawStandardOutput);
        Assert.Equal(["list failed"], result.RawStandardError);
        Assert.Contains("list failed", result.Diagnostics[0], StringComparison.Ordinal);
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
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"dotnet-scaffold-studio-template-{Guid.NewGuid():N}");
            Directory.CreateDirectory(path);
            return new TemporaryDirectory(path);
        }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
