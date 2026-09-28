using DotNetScaffoldStudio.Domain;

namespace DotNetScaffoldStudio.Application;

public sealed class CommandExecutionWorkflow(
    CommandCoordinator coordinator,
    IGitStatusService gitStatusService,
    IFileSnapshotService fileSnapshotService,
    IExecutionHistory? executionHistory = null) : ICommandExecutionWorkflow
{
    private readonly SemaphoreSlim _mutationWorkflowSlot = new(1, 1);

    public async Task<CommandExecutionResult> ExecuteAsync(
        string workspaceRoot,
        CommandRequest request,
        IProgress<CommandOutputLine>? progress,
        CancellationToken cancellationToken)
    {
        if (!request.ModifiesWorkspace)
        {
            var readOnlyResult = await coordinator.ExecuteAsync(request, progress, cancellationToken);
            executionHistory?.Record(request, new CommandExecutionResult(readOnlyResult, null));
            return new CommandExecutionResult(readOnlyResult, null);
        }

        await _mutationWorkflowSlot.WaitAsync(cancellationToken);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var beforeFiles = await fileSnapshotService.CaptureAsync(workspaceRoot, cancellationToken);
            var beforeGit = await gitStatusService.GetStatusAsync(workspaceRoot, cancellationToken);
            var command = await coordinator.ExecuteAsync(request, progress, cancellationToken);

            // Cancellation stops the command, but must not stop scans that report partial output.
            var afterFiles = await fileSnapshotService.CaptureAsync(workspaceRoot, CancellationToken.None);
            var afterGit = await gitStatusService.GetStatusAsync(workspaceRoot, CancellationToken.None);
            var fileChanges = fileSnapshotService.Compare(beforeFiles, afterFiles);
            var differences = ExecutionDifferenceAggregator.Create(beforeGit, afterGit, fileChanges);
            var execution = new CommandExecutionResult(command, differences);
            executionHistory?.Record(request, execution);
            return execution;
        }
        finally
        {
            _mutationWorkflowSlot.Release();
        }
    }
}
