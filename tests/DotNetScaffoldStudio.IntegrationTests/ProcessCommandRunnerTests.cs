using DotNetScaffoldStudio.CommandProbe;
using DotNetScaffoldStudio.Domain;
using DotNetScaffoldStudio.Infrastructure;

namespace DotNetScaffoldStudio.IntegrationTests;

public sealed class ProcessCommandRunnerTests
{
    [Fact]
    public async Task RunAsync_StreamsBothChannelsAndDoesNotDeadlockOnLargeOutput()
    {
        using var workspace = TemporaryDirectory.Create();
        var progressLines = new List<CommandOutputLine>();
        var request = CreateProbeRequest(workspace.Path, [new CommandArgument("mixed")]);

        var result = await new ProcessCommandRunner().RunAsync(
            request,
            new SynchronousProgress<CommandOutputLine>(progressLines.Add),
            CancellationToken.None);

        Assert.Equal(23, result.ExitCode);
        Assert.Equal(4096, result.StandardOutput.Count);
        Assert.Equal(4096, result.StandardError.Count);
        Assert.Contains(progressLines, line => line.Stream == CommandOutputStream.StandardOutput);
        Assert.Contains(progressLines, line => line.Stream == CommandOutputStream.StandardError);
    }

    [Fact]
    public async Task RunAsync_RedactsSecretsFromBothOutputChannels()
    {
        using var workspace = TemporaryDirectory.Create();
        const string secret = "Server=private;Password=do-not-log";
        var request = CreateProbeRequest(
            workspace.Path,
            [new CommandArgument("secret"), new CommandArgument(secret, isSecret: true)]);

        var result = await new ProcessCommandRunner().RunAsync(request, null, CancellationToken.None);
        var persistedText = string.Join('\n', result.StandardOutput.Concat(result.StandardError));

        Assert.DoesNotContain(secret, persistedText, StringComparison.Ordinal);
        Assert.Contains("••••••", persistedText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_CancellationTerminatesLongRunningProcess()
    {
        using var workspace = TemporaryDirectory.Create();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(250));
        var request = CreateProbeRequest(workspace.Path, [new CommandArgument("wait")]);

        var result = await new ProcessCommandRunner().RunAsync(request, null, cancellation.Token);

        Assert.True(result.WasCancelled);
        Assert.False(result.Succeeded);
    }

    private static CommandRequest CreateProbeRequest(string workingDirectory, IReadOnlyList<CommandArgument> probeArguments)
    {
        var dotnetHost = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? Environment.ProcessPath ?? "dotnet";
        return new CommandRequest(
            dotnetHost,
            [new CommandArgument(typeof(ProbeMarker).Assembly.Location), .. probeArguments],
            workingDirectory,
            FeatureRisk.Normal,
            modifiesWorkspace: false);
    }

    private sealed class SynchronousProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        private TemporaryDirectory(string path) => Path = path;

        public string Path { get; }

        public static TemporaryDirectory Create()
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"dotnet-scaffold-studio-{Guid.NewGuid():N}");
            Directory.CreateDirectory(path);
            return new TemporaryDirectory(path);
        }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
