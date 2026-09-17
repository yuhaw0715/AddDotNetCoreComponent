using DotNetScaffoldStudio.Domain;

namespace DotNetScaffoldStudio.Application;

public sealed class CommandAlreadyRunningException : InvalidOperationException
{
    public CommandAlreadyRunningException()
        : base("目前已有會修改工作區的命令正在執行。")
    {
    }
}

public sealed class CommandCoordinator
{
    private readonly ICommandRunner _runner;
    private readonly SemaphoreSlim _readOnlySlots;
    private int _mutationActive;

    public CommandCoordinator(ICommandRunner runner, int maximumConcurrentReadOnlyCommands = 2)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumConcurrentReadOnlyCommands, 1);
        _runner = runner;
        _readOnlySlots = new SemaphoreSlim(maximumConcurrentReadOnlyCommands, maximumConcurrentReadOnlyCommands);
    }

    public async Task<CommandResult> ExecuteAsync(
        CommandRequest request,
        IProgress<CommandOutputLine>? progress,
        CancellationToken cancellationToken)
    {
        if (request.ModifiesWorkspace)
        {
            if (Interlocked.CompareExchange(ref _mutationActive, 1, 0) != 0)
            {
                throw new CommandAlreadyRunningException();
            }

            try
            {
                return await _runner.RunAsync(request, progress, cancellationToken);
            }
            finally
            {
                Volatile.Write(ref _mutationActive, 0);
            }
        }

        await _readOnlySlots.WaitAsync(cancellationToken);
        try
        {
            return await _runner.RunAsync(request, progress, cancellationToken);
        }
        finally
        {
            _readOnlySlots.Release();
        }
    }
}
