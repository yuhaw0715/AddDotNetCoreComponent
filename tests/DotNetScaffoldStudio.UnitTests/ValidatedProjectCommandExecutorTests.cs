using DotNetScaffoldStudio.Application;
using DotNetScaffoldStudio.Domain;

namespace DotNetScaffoldStudio.UnitTests;

public sealed class ValidatedProjectCommandExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_WhenProjectMovedDoesNotStartCommand()
    {
        var runner = new RecordingRunner();
        var executor = new ValidatedProjectCommandExecutor(
            new RejectedValidator(),
            new CommandCoordinator(runner));
        var request = new CommandRequest(
            "dotnet",
            [new CommandArgument("new")],
            "/tmp/workspace",
            FeatureRisk.Normal,
            modifiesWorkspace: true);

        var exception = await Assert.ThrowsAsync<TargetProjectInvalidException>(() =>
            executor.ExecuteAsync(
                "/tmp/workspace",
                "/tmp/workspace/Moved.csproj",
                request,
                null,
                CancellationToken.None));

        Assert.Contains("移動", exception.Message, StringComparison.Ordinal);
        Assert.Equal(0, runner.InvocationCount);
    }

    private sealed class RejectedValidator : ITargetProjectValidator
    {
        public TargetValidationResult Validate(string workspaceRoot, string projectPath, string commandWorkingDirectory) =>
            new(false, "目標專案已移動，請重新掃描。");
    }

    private sealed class RecordingRunner : ICommandRunner
    {
        public int InvocationCount { get; private set; }

        public Task<CommandResult> RunAsync(
            CommandRequest request,
            IProgress<CommandOutputLine>? progress,
            CancellationToken cancellationToken)
        {
            InvocationCount++;
            var now = DateTimeOffset.UtcNow;
            return Task.FromResult(new CommandResult(0, now, now, [], []));
        }
    }
}
