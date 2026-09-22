using DotNetScaffoldStudio.Application;
using DotNetScaffoldStudio.Domain;

namespace DotNetScaffoldStudio.UnitTests;

public sealed class GenerationWorkflowTests
{
    private const string WorkspaceRoot = "/tmp/generation-workflow-workspace";
    private const string ProjectPath = "/tmp/generation-workflow-workspace/App.csproj";

    [Fact]
    public async Task MissingDependency_RejectsBeforeExecution()
    {
        var feature = OfficialFeatureCatalog.DotNetNew.Single(item => item.Id == "webapi");
        var request = new CommandRequest("dotnet", [new CommandArgument("new")], WorkspaceRoot, FeatureRisk.Normal, true);
        var execution = new RecordingExecutionWorkflow();
        var workflow = CreateWorkflow(
            execution,
            new FixtureDependencyDiscovery(EnvironmentDetectionStatus.Missing));

        var plan = await workflow.CreatePlanAsync(
            feature,
            request,
            WorkspaceRoot,
            ProjectPath,
            [],
            CancellationToken.None);
        var result = await workflow.ExecuteAsync(plan, null, null, CancellationToken.None);

        Assert.Equal(GenerationPlanStatus.DependenciesUnavailable, plan.Status);
        Assert.Equal(GenerationExecutionStatus.PreflightRejected, result.Status);
        Assert.Equal(0, execution.InvocationCount);
    }

    [Fact]
    public async Task DatabaseUpdate_RequiresConfirmationBeforeExecution()
    {
        var feature = OfficialFeatureCatalog.EfCore.Single(item => item.Id == "database-update");
        var request = new CommandRequest(
            "dotnet",
            [new CommandArgument("ef"), new CommandArgument("database"), new CommandArgument("update")],
            WorkspaceRoot,
            FeatureRisk.DatabaseChange,
            modifiesWorkspace: true);
        var execution = new RecordingExecutionWorkflow();
        var workflow = CreateWorkflow(execution, new FixtureDependencyDiscovery(EnvironmentDetectionStatus.Available));

        var plan = await workflow.CreatePlanAsync(
            feature,
            request,
            WorkspaceRoot,
            ProjectPath,
            [],
            CancellationToken.None);
        var result = await workflow.ExecuteAsync(plan, null, null, CancellationToken.None);

        Assert.Equal(GenerationExecutionStatus.ConfirmationRejected, result.Status);
        Assert.Contains("確認", result.Diagnostics.Single(), StringComparison.Ordinal);
        Assert.Equal(0, execution.InvocationCount);
    }

    [Fact]
    public async Task Cancellation_ReturnsCancelledWithoutClaimingSuccess()
    {
        var feature = OfficialFeatureCatalog.DotNetNew.Single(item => item.Id == "webapi");
        var request = new CommandRequest("dotnet", [new CommandArgument("new")], WorkspaceRoot, FeatureRisk.Normal, true);
        var execution = new RecordingExecutionWorkflow(throwCancellation: true);
        var workflow = CreateWorkflow(execution, new FixtureDependencyDiscovery(EnvironmentDetectionStatus.Available));

        var plan = await workflow.CreatePlanAsync(
            feature,
            request,
            WorkspaceRoot,
            ProjectPath,
            [],
            CancellationToken.None);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var result = await workflow.ExecuteAsync(plan, null, null, cancellation.Token);

        Assert.Equal(GenerationExecutionStatus.Cancelled, result.Status);
        Assert.False(result.Succeeded);
        Assert.Equal(1, execution.InvocationCount);
    }

    private static GenerationWorkflow CreateWorkflow(
        RecordingExecutionWorkflow execution,
        FixtureDependencyDiscovery discovery) =>
        new(
            discovery,
            new AlwaysValidTargetValidator(),
            new ExpectedOutputConflictChecker(),
            execution,
            new CommandRiskPolicy());

    private sealed class FixtureDependencyDiscovery(EnvironmentDetectionStatus status) : IDependencyDiscovery
    {
        public Task<DependencyDiscoveryResult> DiscoverAsync(
            string workspaceRoot,
            string? targetProjectPath,
            IReadOnlyList<DependencyRequirement> requirements,
            CancellationToken cancellationToken)
        {
            var capabilities = requirements
                .Select(requirement => new DependencyCapability(
                    requirement,
                    status,
                    status == EnvironmentDetectionStatus.Available ? requirement.MinimumVersion : null,
                    "fixture",
                    status == EnvironmentDetectionStatus.Available ? null : "fixture 缺少相依性。"))
                .ToArray();
            return Task.FromResult(new DependencyDiscoveryResult(
                status,
                capabilities,
                status == EnvironmentDetectionStatus.Available ? [] : ["fixture 缺少相依性。"]));
        }
    }

    private sealed class AlwaysValidTargetValidator : ITargetProjectValidator
    {
        public TargetValidationResult Validate(string workspaceRoot, string projectPath, string commandWorkingDirectory) =>
            TargetValidationResult.Valid;
    }

    private sealed class RecordingExecutionWorkflow(bool throwCancellation = false) : ICommandExecutionWorkflow
    {
        public int InvocationCount { get; private set; }

        public Task<CommandExecutionResult> ExecuteAsync(
            string workspaceRoot,
            CommandRequest request,
            IProgress<CommandOutputLine>? progress,
            CancellationToken cancellationToken)
        {
            InvocationCount++;
            if (throwCancellation)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            var now = DateTimeOffset.UtcNow;
            return Task.FromResult(new CommandExecutionResult(
                new CommandResult(0, now, now, [], []),
                new ExecutionDifferenceSummary([], [], [])));
        }
    }
}
