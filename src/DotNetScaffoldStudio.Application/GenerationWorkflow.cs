using DotNetScaffoldStudio.Domain;

namespace DotNetScaffoldStudio.Application;

public sealed class GenerationWorkflow(
    IDependencyDiscovery dependencyDiscovery,
    ITargetProjectValidator targetProjectValidator,
    ExpectedOutputConflictChecker conflictChecker,
    ICommandExecutionWorkflow executionWorkflow,
    CommandRiskPolicy riskPolicy)
{
    public async Task<GenerationPlan> CreatePlanAsync(
        CatalogFeature feature,
        CommandRequest request,
        string workspaceRoot,
        string targetProjectPath,
        IReadOnlyCollection<string> existingOutputPaths,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(feature);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetProjectPath);
        ArgumentNullException.ThrowIfNull(existingOutputPaths);

        var root = Path.GetFullPath(workspaceRoot);
        var project = Path.GetFullPath(targetProjectPath, root);
        var targetValidation = targetProjectValidator.Validate(root, project, request.WorkingDirectory);
        var diagnostics = new List<string>();
        DependencyDiscoveryResult dependencies;
        if (!targetValidation.IsValid)
        {
            dependencies = new DependencyDiscoveryResult(
                EnvironmentDetectionStatus.DetectionFailed,
                [],
                [targetValidation.ErrorMessage ?? "目標專案無效，未進行相依性探索。"]);
            diagnostics.Add(targetValidation.ErrorMessage ?? "目標專案無效。");
        }
        else
        {
            dependencies = await dependencyDiscovery.DiscoverAsync(
                root,
                project,
                feature.Dependencies,
                cancellationToken);
            diagnostics.AddRange(dependencies.Diagnostics);
        }

        var outputConflict = conflictChecker.Check(request, existingOutputPaths);
        if (outputConflict.Status != OutputConflictStatus.NoConflict)
        {
            diagnostics.Add(outputConflict.Message);
        }

        return new GenerationPlan(
            root,
            project,
            feature,
            request,
            dependencies,
            targetValidation,
            outputConflict,
            diagnostics.AsReadOnly());
    }

    public async Task<GenerationExecutionResult> ExecuteAsync(
        GenerationPlan plan,
        ConfirmationToken? confirmation,
        IProgress<CommandOutputLine>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);

        if (!plan.CanExecute)
        {
            return new GenerationExecutionResult(
                GenerationExecutionStatus.PreflightRejected,
                plan,
                null,
                plan.Diagnostics);
        }

        if (!riskPolicy.ValidateAndConsume(plan.Command, confirmation))
        {
            return new GenerationExecutionResult(
                GenerationExecutionStatus.ConfirmationRejected,
                plan,
                null,
                ["命令尚未取得有效確認，未啟動外部程序。"]);
        }

        try
        {
            var execution = await new ValidatedProjectCommandExecutor(
                targetProjectValidator,
                executionWorkflow).ExecuteAsync(
                plan.WorkspaceRoot,
                plan.TargetProjectPath,
                plan.Command,
                progress,
                cancellationToken);
            var status = execution.Command.WasCancelled
                ? GenerationExecutionStatus.Cancelled
                : execution.Command.Succeeded
                    ? GenerationExecutionStatus.Succeeded
                    : GenerationExecutionStatus.Failed;
            return new GenerationExecutionResult(status, plan, execution, plan.Diagnostics);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new GenerationExecutionResult(
                GenerationExecutionStatus.Cancelled,
                plan,
                null,
                [.. plan.Diagnostics, "使用者取消了產生流程。"]);
        }
        catch (TargetProjectInvalidException exception)
        {
            return new GenerationExecutionResult(
                GenerationExecutionStatus.PreflightRejected,
                plan,
                null,
                [.. plan.Diagnostics, exception.Message]);
        }
    }
}
