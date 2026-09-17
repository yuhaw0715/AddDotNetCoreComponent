using DotNetScaffoldStudio.Application;
using DotNetScaffoldStudio.Domain;

namespace DotNetScaffoldStudio.UnitTests;

public sealed class CommandCoordinatorTests
{
    [Fact]
    public async Task ExecuteAsync_RejectsSecondMutationAndRecoversAfterFirstCompletes()
    {
        var runner = new BlockingRunner();
        var coordinator = new CommandCoordinator(runner);
        var request = CreateRequest(modifiesWorkspace: true);

        var first = coordinator.ExecuteAsync(request, null, CancellationToken.None);
        await runner.Started.Task.WaitAsync(TimeSpan.FromSeconds(1));

        await Assert.ThrowsAsync<CommandAlreadyRunningException>(() =>
            coordinator.ExecuteAsync(request, null, CancellationToken.None));

        runner.Release.TrySetResult();
        await first;
        await coordinator.ExecuteAsync(request, null, CancellationToken.None);

        Assert.Equal(2, runner.CompletedCount);
    }

    [Fact]
    public async Task ExecuteAsync_LimitsConcurrentReadOnlyCommands()
    {
        var runner = new CountingRunner();
        var coordinator = new CommandCoordinator(runner, maximumConcurrentReadOnlyCommands: 2);
        var request = CreateRequest(modifiesWorkspace: false);

        var commands = Enumerable.Range(0, 6)
            .Select(_ => coordinator.ExecuteAsync(request, null, CancellationToken.None))
            .ToArray();
        await Task.WhenAll(commands);

        Assert.Equal(2, runner.MaximumConcurrency);
    }

    private static CommandRequest CreateRequest(bool modifiesWorkspace) =>
        new("dotnet", [new CommandArgument("--info")], "/tmp", FeatureRisk.Normal, modifiesWorkspace);

    private sealed class BlockingRunner : ICommandRunner
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int CompletedCount { get; private set; }

        public async Task<CommandResult> RunAsync(
            CommandRequest request,
            IProgress<CommandOutputLine>? progress,
            CancellationToken cancellationToken)
        {
            Started.TrySetResult();
            if (CompletedCount == 0)
            {
                await Release.Task.WaitAsync(cancellationToken);
            }

            CompletedCount++;
            return Success();
        }
    }

    private sealed class CountingRunner : ICommandRunner
    {
        private int _concurrency;
        public int MaximumConcurrency { get; private set; }

        public async Task<CommandResult> RunAsync(
            CommandRequest request,
            IProgress<CommandOutputLine>? progress,
            CancellationToken cancellationToken)
        {
            var concurrency = Interlocked.Increment(ref _concurrency);
            MaximumConcurrency = Math.Max(MaximumConcurrency, concurrency);
            await Task.Delay(40, cancellationToken);
            Interlocked.Decrement(ref _concurrency);
            return Success();
        }
    }

    private static CommandResult Success()
    {
        var now = DateTimeOffset.UtcNow;
        return new CommandResult(0, now, now, [], []);
    }
}
