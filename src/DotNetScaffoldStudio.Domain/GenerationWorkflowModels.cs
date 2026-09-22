namespace DotNetScaffoldStudio.Domain;

public enum GenerationPlanStatus
{
    Ready,
    TargetInvalid,
    DependenciesUnavailable,
    OutputConflict
}

public sealed record GenerationPlan(
    string WorkspaceRoot,
    string TargetProjectPath,
    CatalogFeature Feature,
    CommandRequest Command,
    DependencyDiscoveryResult Dependencies,
    TargetValidationResult TargetValidation,
    OutputConflictResult OutputConflict,
    IReadOnlyList<string> Diagnostics)
{
    public GenerationPlanStatus Status =>
        !TargetValidation.IsValid
            ? GenerationPlanStatus.TargetInvalid
            : !Dependencies.IsFullyAvailable
                ? GenerationPlanStatus.DependenciesUnavailable
                : OutputConflict.Status == OutputConflictStatus.Blocked
                    ? GenerationPlanStatus.OutputConflict
                    : GenerationPlanStatus.Ready;

    public bool CanExecute => Status == GenerationPlanStatus.Ready;
}

public enum GenerationExecutionStatus
{
    Succeeded,
    Failed,
    Cancelled,
    ConfirmationRejected,
    PreflightRejected
}

public sealed record GenerationExecutionResult(
    GenerationExecutionStatus Status,
    GenerationPlan Plan,
    CommandExecutionResult? Execution,
    IReadOnlyList<string> Diagnostics)
{
    public bool Succeeded => Status == GenerationExecutionStatus.Succeeded;
}
