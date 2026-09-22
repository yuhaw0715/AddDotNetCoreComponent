using DotNetScaffoldStudio.Application;
using DotNetScaffoldStudio.Domain;

namespace DotNetScaffoldStudio.UnitTests;

public sealed class DependencyPlanWorkflowTests
{
    [Fact]
    public async Task ExecuteAsync_WhenConfirmationIsRejected_DoesNotExecuteOrRevalidate()
    {
        var plan = CreatePlan(2);
        var execution = new FixtureExecutionWorkflow(_ => SuccessfulExecution());
        var discovery = new FixtureDependencyDiscovery(_ => Available("unused"));
        var workflow = new DependencyPlanWorkflow(
            execution,
            discovery,
            new DependencyPlanConfirmationPolicy());

        var result = await workflow.ExecuteAsync(plan, null, null, CancellationToken.None);

        Assert.Equal(DependencyPlanExecutionStatus.ConfirmationRejected, result.Status);
        Assert.True(result.ConfirmationRejected);
        Assert.Empty(result.Steps);
        Assert.Empty(execution.Requests);
        Assert.Empty(discovery.Requests);
        Assert.Null(result.Differences);
    }

    [Fact]
    public async Task ExecuteAsync_WhenALaterStepFails_ReportsPartialSuccessAndPreservesDifferences()
    {
        var plan = CreatePlan(3);
        var execution = new FixtureExecutionWorkflow(request =>
        {
            var stepId = request.Arguments[0].Value;
            return stepId == "tool-2"
                ? FailedExecution(17, new FileChange(FileChangeKind.Added, "partial-tool.json"))
                : SuccessfulExecution(new FileChange(FileChangeKind.Added, $"{stepId}.json"));
        });
        var discovery = new FixtureDependencyDiscovery(requirement => Available(requirement.Id));
        var policy = new DependencyPlanConfirmationPolicy();
        var workflow = new DependencyPlanWorkflow(execution, discovery, policy);

        var result = await workflow.ExecuteAsync(plan, policy.Issue(plan), null, CancellationToken.None);

        Assert.Equal(DependencyPlanExecutionStatus.PartiallySucceeded, result.Status);
        Assert.Equal(
            [
                DependencyPlanStepStatus.Succeeded,
                DependencyPlanStepStatus.Failed,
                DependencyPlanStepStatus.Skipped
            ],
            result.Steps.Select(step => step.Status));
        Assert.Equal(2, execution.Requests.Count);
        Assert.Equal(2, discovery.Requests.Count);
        Assert.Contains(
            new FileChange(FileChangeKind.Added, "tool-1.json"),
            result.Differences!.CommandFileChanges);
        Assert.Contains(
            new FileChange(FileChangeKind.Added, "partial-tool.json"),
            result.Differences.CommandFileChanges);
        Assert.Contains("未自動回復", result.Diagnostics.Single(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCommandSucceedsButRevalidationFails_ReportsFailureAndKeepsDifference()
    {
        var plan = CreatePlan(1);
        var execution = new FixtureExecutionWorkflow(_ =>
            SuccessfulExecution(new FileChange(FileChangeKind.Modified, "project.csproj")));
        var discovery = new FixtureDependencyDiscovery(requirement =>
            new DependencyDiscoveryResult(
                EnvironmentDetectionStatus.Incompatible,
                [new DependencyCapability(
                    requirement,
                    EnvironmentDetectionStatus.Incompatible,
                    "9.0.0",
                    "fixture",
                    "主要版本不相容。")],
                ["主要版本不相容。"]));
        var policy = new DependencyPlanConfirmationPolicy();
        var workflow = new DependencyPlanWorkflow(execution, discovery, policy);

        var result = await workflow.ExecuteAsync(plan, policy.Issue(plan), null, CancellationToken.None);

        Assert.Equal(DependencyPlanExecutionStatus.RevalidationFailed, result.Status);
        var step = Assert.Single(result.Steps);
        Assert.Equal(DependencyPlanStepStatus.RevalidationFailed, step.Status);
        Assert.Equal(EnvironmentDetectionStatus.Incompatible, step.Revalidation!.Status);
        Assert.Contains(
            new FileChange(FileChangeKind.Modified, "project.csproj"),
            result.Differences!.CommandFileChanges);
        Assert.Contains("重驗證失敗", result.Diagnostics.Single(), StringComparison.Ordinal);
    }

    [Fact]
    public void ConfirmationPolicy_RejectsChangedPlanAndConsumesValidConfirmationOnce()
    {
        var firstPlan = CreatePlan(1);
        var changedPlan = CreatePlan(1, expectedChange: "different.json");
        var policy = new DependencyPlanConfirmationPolicy();
        var confirmation = policy.Issue(firstPlan);

        Assert.False(policy.ValidateAndConsume(changedPlan, confirmation));
        Assert.True(policy.ValidateAndConsume(firstPlan, confirmation));
        Assert.False(policy.ValidateAndConsume(firstPlan, confirmation));
    }

    private static DependencyPlan CreatePlan(int count, string? expectedChange = null)
    {
        var workspace = "/tmp/dependency-plan-workspace";
        var capabilities = Enumerable.Range(1, count)
            .Select(index => new DependencyCapability(
                new DependencyRequirement(DependencyKind.LocalTool, $"tool-{index}", "10.0"),
                EnvironmentDetectionStatus.Missing,
                null,
                "fixture",
                "缺少工具。"))
            .ToArray();
        var steps = capabilities
            .Select(capability => new DependencyPlanStep(
                capability,
                new CommandRequest(
                    "dotnet",
                    [new CommandArgument(capability.Requirement.Id)],
                    workspace,
                    FeatureRisk.EnvironmentChange,
                    modifiesWorkspace: true),
                requiresNetwork: true,
                expectedChanges: [expectedChange ?? $"{capability.Requirement.Id}.json"]))
            .ToArray();
        return new DependencyPlan(workspace, null, capabilities, steps);
    }

    private static DependencyDiscoveryResult Available(string id) =>
        new(
            EnvironmentDetectionStatus.Available,
            [new DependencyCapability(
                new DependencyRequirement(DependencyKind.LocalTool, id, "10.0"),
                EnvironmentDetectionStatus.Available,
                "10.0.0",
                "fixture",
                null)],
            []);

    private static CommandExecutionResult SuccessfulExecution(params FileChange[] fileChanges) =>
        new(
            new CommandResult(
                0,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow,
                ["ok"],
                []),
            new ExecutionDifferenceSummary(
                [new GitFileChange(" M", "existing.csproj")],
                [new GitFileChange("??", fileChanges.FirstOrDefault()?.RelativePath ?? "none")],
                fileChanges));

    private static CommandExecutionResult FailedExecution(int exitCode, params FileChange[] fileChanges) =>
        new(
            new CommandResult(
                exitCode,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow,
                [],
                ["fixture failed"]),
            new ExecutionDifferenceSummary(
                [new GitFileChange(" M", "existing.csproj")],
                [new GitFileChange(" M", "partial.csproj")],
                fileChanges));

    private sealed class FixtureExecutionWorkflow(
        Func<CommandRequest, CommandExecutionResult> respond) : ICommandExecutionWorkflow
    {
        public List<CommandRequest> Requests { get; } = [];

        public Task<CommandExecutionResult> ExecuteAsync(
            string workspaceRoot,
            CommandRequest request,
            IProgress<CommandOutputLine>? progress,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(request);
            return Task.FromResult(respond(request));
        }
    }

    private sealed class FixtureDependencyDiscovery(
        Func<DependencyRequirement, DependencyDiscoveryResult> respond) : IDependencyDiscovery
    {
        public List<DependencyRequirement> Requests { get; } = [];

        public Task<DependencyDiscoveryResult> DiscoverAsync(
            string workspaceRoot,
            string? targetProjectPath,
            IReadOnlyList<DependencyRequirement> requirements,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var requirement = Assert.Single(requirements);
            Requests.Add(requirement);
            return Task.FromResult(respond(requirement));
        }
    }
}
