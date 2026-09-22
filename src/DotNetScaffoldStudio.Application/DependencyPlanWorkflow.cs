using DotNetScaffoldStudio.Domain;

namespace DotNetScaffoldStudio.Application;

public sealed class DependencyPlanConfirmationPolicy
{
    private readonly HashSet<Guid> _activeTokens = [];

    public DependencyPlanConfirmation Issue(DependencyPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (!plan.RequiresConfirmation)
        {
            throw new InvalidOperationException("沒有待執行的相依性步驟，不需要確認 token。");
        }

        var token = new DependencyPlanConfirmation(Guid.NewGuid(), plan.GetConfirmationFingerprint());
        _activeTokens.Add(token.Id);
        return token;
    }

    public bool ValidateAndConsume(DependencyPlan plan, DependencyPlanConfirmation? confirmation)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (!plan.RequiresConfirmation)
        {
            return true;
        }

        return confirmation is not null &&
               string.Equals(
                   confirmation.PlanFingerprint,
                   plan.GetConfirmationFingerprint(),
                   StringComparison.Ordinal) &&
               _activeTokens.Remove(confirmation.Id);
    }
}

public sealed record DependencyPlanStepResult(
    DependencyPlanStep Step,
    DependencyPlanStepStatus Status,
    CommandExecutionResult? Execution,
    DependencyDiscoveryResult? Revalidation,
    string? Diagnostic);

public sealed record DependencyPlanExecutionResult(
    DependencyPlanExecutionStatus Status,
    IReadOnlyList<DependencyPlanStepResult> Steps,
    DependencyDiscoveryResult? FinalRevalidation,
    ExecutionDifferenceSummary? Differences,
    IReadOnlyList<string> Diagnostics)
{
    public bool ConfirmationRejected => Status == DependencyPlanExecutionStatus.ConfirmationRejected;
}

public sealed class DependencyPlanWorkflow(
    ICommandExecutionWorkflow executionWorkflow,
    IDependencyDiscovery dependencyDiscovery,
    DependencyPlanConfirmationPolicy confirmationPolicy) : IDependencyPlanWorkflow
{
    public async Task<DependencyPlanExecutionResult> ExecuteAsync(
        DependencyPlan plan,
        DependencyPlanConfirmation? confirmation,
        IProgress<CommandOutputLine>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(confirmationPolicy);

        if (!confirmationPolicy.ValidateAndConsume(plan, confirmation))
        {
            return new DependencyPlanExecutionResult(
                DependencyPlanExecutionStatus.ConfirmationRejected,
                [],
                null,
                null,
                ["相依性計畫尚未取得有效確認，未執行任何步驟。"]);
        }

        if (!plan.RequiresConfirmation)
        {
            return new DependencyPlanExecutionResult(
                DependencyPlanExecutionStatus.Succeeded,
                [],
                null,
                null,
                []);
        }

        var results = new List<DependencyPlanStepResult>(plan.Steps.Count);
        var diagnostics = new List<string>();
        foreach (var step in plan.Steps)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DependencyPlanStepResult stepResult;
            try
            {
                var execution = await executionWorkflow.ExecuteAsync(
                    plan.WorkspaceRoot,
                    step.Request,
                    progress,
                    cancellationToken);
                var revalidation = await dependencyDiscovery.DiscoverAsync(
                    plan.WorkspaceRoot,
                    plan.TargetProjectPath,
                    [step.Dependency.Requirement],
                    cancellationToken);
                stepResult = CreateStepResult(step, execution, revalidation);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception) when (exception is IOException or InvalidOperationException)
            {
                stepResult = new DependencyPlanStepResult(
                    step,
                    DependencyPlanStepStatus.Failed,
                    null,
                    null,
                    $"執行相依性步驟失敗：{exception.Message}");
            }

            results.Add(stepResult);
            if (stepResult.Diagnostic is not null)
            {
                diagnostics.Add(stepResult.Diagnostic);
            }

            if (stepResult.Status != DependencyPlanStepStatus.Succeeded)
            {
                break;
            }
        }

        if (results.Count < plan.Steps.Count)
        {
            foreach (var skippedStep in plan.Steps.Skip(results.Count))
            {
                results.Add(new DependencyPlanStepResult(
                    skippedStep,
                    DependencyPlanStepStatus.Skipped,
                    null,
                    null,
                    "前一個相依性步驟未完成，因此略過。"));
            }
        }

        var finalRevalidation = results.LastOrDefault(result => result.Revalidation is not null)?.Revalidation;
        var status = GetOverallStatus(results);
        return new DependencyPlanExecutionResult(
            status,
            results,
            finalRevalidation,
            MergeDifferences(results),
            diagnostics);
    }

    private static DependencyPlanStepResult CreateStepResult(
        DependencyPlanStep step,
        CommandExecutionResult execution,
        DependencyDiscoveryResult revalidation)
    {
        if (!execution.Command.Succeeded)
        {
            return new DependencyPlanStepResult(
                step,
                DependencyPlanStepStatus.Failed,
                execution,
                revalidation,
                $"相依性命令失敗，結束碼 {execution.Command.ExitCode}。未自動回復已發生的檔案變更。");
        }

        var capability = revalidation.Capabilities.FirstOrDefault(candidate =>
            candidate.Requirement.Kind == step.Dependency.Requirement.Kind &&
            candidate.Requirement.Id.Equals(step.Dependency.Requirement.Id, StringComparison.OrdinalIgnoreCase));
        if (capability is null || !capability.IsAvailable)
        {
            var reason = capability?.Reason ?? "重驗證結果缺少此相依性。";
            return new DependencyPlanStepResult(
                step,
                DependencyPlanStepStatus.RevalidationFailed,
                execution,
                revalidation,
                $"相依性命令完成，但重驗證失敗：{reason}");
        }

        return new DependencyPlanStepResult(step, DependencyPlanStepStatus.Succeeded, execution, revalidation, null);
    }

    private static DependencyPlanExecutionStatus GetOverallStatus(
        IReadOnlyList<DependencyPlanStepResult> results)
    {
        if (results.Any(result => result.Status == DependencyPlanStepStatus.RevalidationFailed))
        {
            return DependencyPlanExecutionStatus.RevalidationFailed;
        }

        if (results.Any(result => result.Status == DependencyPlanStepStatus.Failed))
        {
            return results.Any(result => result.Status == DependencyPlanStepStatus.Succeeded)
                ? DependencyPlanExecutionStatus.PartiallySucceeded
                : DependencyPlanExecutionStatus.ExecutionFailed;
        }

        return results.All(result => result.Status == DependencyPlanStepStatus.Succeeded)
            ? DependencyPlanExecutionStatus.Succeeded
            : DependencyPlanExecutionStatus.PartiallySucceeded;
    }

    private static ExecutionDifferenceSummary? MergeDifferences(
        IReadOnlyList<DependencyPlanStepResult> results)
    {
        var differences = results
            .Select(result => result.Execution?.Differences)
            .Where(difference => difference is not null)
            .Cast<ExecutionDifferenceSummary>()
            .ToArray();
        if (differences.Length == 0)
        {
            return null;
        }

        var existing = differences[0].ExistingGitChanges;
        var current = differences
            .SelectMany(difference => difference.CurrentGitChanges)
            .DistinctBy(change => $"{change.Status}\u001f{change.Path}", StringComparer.Ordinal)
            .ToArray();
        var files = differences
            .SelectMany(difference => difference.CommandFileChanges)
            .DistinctBy(change => $"{change.Kind}\u001f{change.RelativePath}", StringComparer.Ordinal)
            .ToArray();
        return new ExecutionDifferenceSummary(existing, current, files);
    }
}
