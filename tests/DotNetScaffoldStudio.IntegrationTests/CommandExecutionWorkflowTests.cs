using System.Diagnostics;
using DotNetScaffoldStudio.Application;
using DotNetScaffoldStudio.CommandProbe;
using DotNetScaffoldStudio.Domain;
using DotNetScaffoldStudio.Infrastructure;

namespace DotNetScaffoldStudio.IntegrationTests;

public sealed class CommandExecutionWorkflowTests
{
    [Fact]
    public async Task ExecuteAsync_WhenCancelled_KillsProcessTreeAndRescansPartialOutput()
    {
        using var workspace = TemporaryDirectory.Create();
        var existingChange = new GitFileChange(" M", "already-changed.cs");
        var generatedChange = new GitFileChange("??", "PartiallyGenerated.cs");
        var gitStatus = new SequencedGitStatusService(
            new GitWorkspaceState(true, "main", [existingChange]),
            new GitWorkspaceState(true, "main", [existingChange, generatedChange]));
        var workflow = new CommandExecutionWorkflow(
            new CommandCoordinator(new ProcessCommandRunner()),
            gitStatus,
            new FileSnapshotService());
        using var cancellation = new CancellationTokenSource();
        var childPidReady = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var progress = new SynchronousProgress<CommandOutputLine>(line =>
        {
            if (line.Stream == CommandOutputStream.StandardOutput && line.Text.StartsWith("CHILD_STARTED ", StringComparison.Ordinal))
            {
                childPidReady.TrySetResult(int.Parse(line.Text[14..], System.Globalization.CultureInfo.InvariantCulture));
            }
        });
        var task = workflow.ExecuteAsync(
            workspace.Path,
            CreateProbeRequest(workspace.Path, [new CommandArgument("partial-tree-wait")]),
            progress,
            cancellation.Token);

        var childPid = await childPidReady.Task.WaitAsync(TimeSpan.FromSeconds(10));
        cancellation.Cancel();
        var result = await task.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.True(result.Command.WasCancelled);
        Assert.True(result.Command.GracefulTerminationAttempted);
        Assert.True(result.Command.WasForceTerminated);
        Assert.Contains("PARTIAL_OUTPUT_WRITTEN", result.Command.StandardOutput);
        Assert.Equal(2, gitStatus.CallCount);
        Assert.Equal([existingChange], result.Differences!.ExistingGitChanges);
        Assert.Equal([generatedChange], result.Differences.CurrentGitChanges);
        Assert.Contains(
            new FileChange(FileChangeKind.Added, "PartiallyGenerated.cs"),
            result.Differences.CommandFileChanges);
        Assert.True(File.Exists(Path.Combine(workspace.Path, "PartiallyGenerated.cs")));
        Assert.True(WaitForProcessExit(childPid, TimeSpan.FromSeconds(5)));
    }

    private static CommandRequest CreateProbeRequest(string workingDirectory, IReadOnlyList<CommandArgument> probeArguments)
    {
        var dotnetHost = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? Environment.ProcessPath ?? "dotnet";
        return new CommandRequest(
            dotnetHost,
            [new CommandArgument(typeof(ProbeMarker).Assembly.Location), .. probeArguments],
            workingDirectory,
            FeatureRisk.Normal,
            modifiesWorkspace: true);
    }

    private static bool WaitForProcessExit(int processId, TimeSpan timeout)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < timeout)
        {
            try
            {
                using var process = Process.GetProcessById(processId);
                if (process.HasExited)
                {
                    return true;
                }
            }
            catch (ArgumentException)
            {
                return true;
            }

            Thread.Sleep(25);
        }

        return false;
    }

    private sealed class SequencedGitStatusService(params GitWorkspaceState[] states) : IGitStatusService
    {
        private readonly Queue<GitWorkspaceState> _states = new(states);

        public int CallCount { get; private set; }

        public Task<GitWorkspaceState> GetStatusAsync(string workspaceRoot, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            return Task.FromResult(_states.Dequeue());
        }
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
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"dotnet-scaffold-studio-workflow-{Guid.NewGuid():N}");
            Directory.CreateDirectory(path);
            return new TemporaryDirectory(path);
        }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
