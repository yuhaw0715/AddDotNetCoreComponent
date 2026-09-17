using System.ComponentModel;
using DotNetScaffoldStudio.Application;
using DotNetScaffoldStudio.Domain;
using DotNetScaffoldStudio.Infrastructure;

namespace DotNetScaffoldStudio.IntegrationTests;

public sealed class GitStatusServiceTests
{
    [Fact]
    public async Task GetStatusAsync_ParsesBranchDirtyAndUntrackedFilesUsingReadOnlyCommands()
    {
        var runner = new QueueRunner(
            Result(0, ["true"]),
            Result(0, ["feature/demo"]),
            Result(0, [" M src/App.cs", "?? notes/demo file.txt"]));
        var service = new GitStatusService(runner);

        var state = await service.GetStatusAsync("/tmp", CancellationToken.None);

        Assert.True(state.IsRepository);
        Assert.Equal("feature/demo", state.Branch);
        Assert.Contains(state.Changes, change => change.Status == " M" && change.Path == "src/App.cs");
        Assert.Contains(state.Changes, change => change.Status == "??" && change.Path == "notes/demo file.txt");
        Assert.All(runner.Commands, command =>
            Assert.DoesNotContain(command.Arguments, argument => argument.Value is "commit" or "stash" or "checkout" or "reset"));
    }

    [Fact]
    public async Task GetStatusAsync_ReturnsCleanRepository()
    {
        var runner = new QueueRunner(Result(0, ["true"]), Result(0, ["main"]), Result(0, []));

        var state = await new GitStatusService(runner).GetStatusAsync("/tmp", CancellationToken.None);

        Assert.True(state.IsRepository);
        Assert.Empty(state.Changes);
    }

    [Fact]
    public async Task GetStatusAsync_NonRepositoryDoesNotRunFurtherGitCommands()
    {
        var runner = new QueueRunner(Result(128, [], ["not a git repository"]));

        var state = await new GitStatusService(runner).GetStatusAsync("/tmp", CancellationToken.None);

        Assert.False(state.IsRepository);
        Assert.Single(runner.Commands);
    }

    [Fact]
    public async Task GetStatusAsync_WhenGitCannotStartReturnsNonRepository()
    {
        var service = new GitStatusService(new UnavailableRunner());

        var state = await service.GetStatusAsync("/tmp", CancellationToken.None);

        Assert.False(state.IsRepository);
        Assert.Null(state.Branch);
        Assert.Empty(state.Changes);
    }

    private static CommandResult Result(int exitCode, IReadOnlyList<string> stdout, IReadOnlyList<string>? stderr = null)
    {
        var now = DateTimeOffset.UtcNow;
        return new CommandResult(exitCode, now, now, stdout, stderr ?? []);
    }

    private sealed class QueueRunner(params CommandResult[] results) : ICommandRunner
    {
        private readonly Queue<CommandResult> _results = new(results);
        public List<CommandRequest> Commands { get; } = [];

        public Task<CommandResult> RunAsync(
            CommandRequest request,
            IProgress<CommandOutputLine>? progress,
            CancellationToken cancellationToken)
        {
            Commands.Add(request);
            return Task.FromResult(_results.Dequeue());
        }
    }

    private sealed class UnavailableRunner : ICommandRunner
    {
        public Task<CommandResult> RunAsync(
            CommandRequest request,
            IProgress<CommandOutputLine>? progress,
            CancellationToken cancellationToken) =>
            Task.FromException<CommandResult>(new Win32Exception("git executable unavailable"));
    }
}
