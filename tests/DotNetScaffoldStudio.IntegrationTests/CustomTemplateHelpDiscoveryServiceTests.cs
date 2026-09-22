using DotNetScaffoldStudio.Application;
using DotNetScaffoldStudio.Domain;
using DotNetScaffoldStudio.Infrastructure;

namespace DotNetScaffoldStudio.IntegrationTests;

public sealed class CustomTemplateHelpDiscoveryServiceTests
{
    [Fact]
    public async Task DiscoverAsync_UsesStructuredHelpRequestAndParsesOutput()
    {
        using var workspace = TemporaryDirectory.Create();
        string[] stdout = [
            "Contoso Template",
            "Template options:",
            "  --name <name>           The generated name.",
            "                          Type: text"
        ];
        var runner = new FixtureRunner(_ => Result(0, stdout));

        var result = await new CustomTemplateHelpDiscoveryService(runner)
            .DiscoverAsync(workspace.Path, "contoso-template", CancellationToken.None);

        var request = Assert.Single(runner.Commands);
        Assert.Equal("dotnet", request.Executable);
        Assert.Equal(["new", "contoso-template", "--help"], request.Arguments.Select(argument => argument.Value));
        Assert.False(request.ModifiesWorkspace);
        Assert.Equal("en", request.Environment["DOTNET_CLI_UI_LANGUAGE"]);
        Assert.Equal("C", request.Environment["LC_ALL"]);
        Assert.Equal(CustomTemplateHelpParseMode.Parsed, result.Mode);
        Assert.False(result.AllowsRestrictedAdditionalArguments);
        Assert.Contains(result.Options, option => option.Name == "--name");
    }

    [Fact]
    public async Task DiscoverAsync_WhenHelpIsPartial_EnablesRestrictedAdditionalArguments()
    {
        using var workspace = TemporaryDirectory.Create();
        string[] stdout = [
            "Template options:",
            "  --known <value>         A known value.",
            "                          Type: text",
            "  --mystery <value>       An unknown value.",
            "                          Type: plugin-specific"
        ];
        var runner = new FixtureRunner(_ => Result(0, stdout));

        var result = await new CustomTemplateHelpDiscoveryService(runner)
            .DiscoverAsync(workspace.Path, "contoso-template", CancellationToken.None);

        Assert.Equal(CustomTemplateHelpParseMode.PartiallyParsed, result.Mode);
        Assert.True(result.AllowsRestrictedAdditionalArguments);
        Assert.Contains(result.Diagnostics, message => message.Contains("未知型別", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DiscoverAsync_RejectsTemplateNamesThatCouldBecomeMultipleArguments()
    {
        using var workspace = TemporaryDirectory.Create();
        var runner = new FixtureRunner(_ => Result(0, []));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            new CustomTemplateHelpDiscoveryService(runner)
                .DiscoverAsync(workspace.Path, "contoso template", CancellationToken.None));

        Assert.Empty(runner.Commands);
    }

    private static CommandResult Result(int exitCode, IReadOnlyList<string> stdout)
    {
        var now = DateTimeOffset.UtcNow;
        return new CommandResult(exitCode, now, now, stdout, []);
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
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"dotnet-scaffold-studio-template-help-{Guid.NewGuid():N}");
            Directory.CreateDirectory(path);
            return new TemporaryDirectory(path);
        }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
