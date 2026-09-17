using DotNetScaffoldStudio.Domain;

namespace DotNetScaffoldStudio.Application;

public sealed class TargetProjectInvalidException(string message) : InvalidOperationException(message);

public sealed class ValidatedProjectCommandExecutor(
    ITargetProjectValidator targetValidator,
    CommandCoordinator coordinator)
{
    public Task<CommandResult> ExecuteAsync(
        string workspaceRoot,
        string projectPath,
        CommandRequest request,
        IProgress<CommandOutputLine>? progress,
        CancellationToken cancellationToken)
    {
        var validation = targetValidator.Validate(workspaceRoot, projectPath, request.WorkingDirectory);
        if (!validation.IsValid)
        {
            throw new TargetProjectInvalidException(validation.ErrorMessage ?? "目標專案無效。");
        }

        return coordinator.ExecuteAsync(request, progress, cancellationToken);
    }
}
