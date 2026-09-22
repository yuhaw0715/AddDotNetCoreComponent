using DotNetScaffoldStudio.Application;
using DotNetScaffoldStudio.Domain;

namespace DotNetScaffoldStudio.UnitTests;

public sealed class DependencyGuidanceTests
{
    [Fact]
    public void Build_WhenSdkAndWorkloadAreMissing_ExplainsDifferentNextSteps()
    {
        var sdk = Capability(DependencyKind.DotNetSdk, "10.0", "找不到 SDK");
        var workload = Capability(DependencyKind.Workload, "wasm-tools", "工作負載不存在");

        var result = new DependencyGuidanceService().Build(
            new DependencyDiscoveryResult(
                EnvironmentDetectionStatus.Missing,
                [sdk, workload],
                ["環境不完整"]));

        var sdkGuidance = Assert.Single(result.Items, item => item.Capability.Requirement.Kind == DependencyKind.DotNetSdk);
        var workloadGuidance = Assert.Single(result.Items, item => item.Capability.Requirement.Kind == DependencyKind.Workload);
        Assert.Equal(DependencyGuidanceKind.DotNetSdkDocumentation, sdkGuidance.Kind);
        Assert.False(sdkGuidance.RequiresIndependentConfirmation);
        Assert.Contains("不會自行下載", sdkGuidance.Description, StringComparison.Ordinal);
        Assert.Contains("dotnet.microsoft.com", sdkGuidance.DocumentationUrl, StringComparison.Ordinal);
        Assert.Equal(DependencyGuidanceKind.WorkloadInstallation, workloadGuidance.Kind);
        Assert.True(workloadGuidance.RequiresIndependentConfirmation);
        Assert.Contains("獨立確認", workloadGuidance.Description, StringComparison.Ordinal);
        Assert.Contains("learn.microsoft.com", workloadGuidance.DocumentationUrl, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_WhenIndependentConfirmationIsMissing_DoesNotCallAdapter()
    {
        var guidance = CreateWorkloadGuidance();
        var adapter = new FixtureWorkloadInstallationAdapter();
        var workflow = new WorkloadInstallationWorkflow(
            adapter,
            new WorkloadInstallationConfirmationPolicy());
        var plan = workflow.CreatePlan(guidance);

        var result = await workflow.ExecuteAsync(plan, null, CancellationToken.None);

        Assert.Equal(WorkloadInstallationExecutionStatus.ConfirmationRejected, result.Status);
        Assert.True(result.ConfirmationRejected);
        Assert.Contains("未啟動任何網路或修改操作", result.Diagnostic, StringComparison.Ordinal);
        Assert.Empty(adapter.Requests);
        Assert.False(adapter.NetworkStarted);
        Assert.False(adapter.WorkspaceModified);
    }

    [Fact]
    public async Task ExecuteAsync_WhenConfirmed_CallsAdapterOnceAndConsumesConfirmation()
    {
        var guidance = CreateWorkloadGuidance();
        var adapter = new FixtureWorkloadInstallationAdapter();
        var policy = new WorkloadInstallationConfirmationPolicy();
        var workflow = new WorkloadInstallationWorkflow(adapter, policy);
        var plan = workflow.CreatePlan(guidance);
        var confirmation = policy.Issue(plan);

        var result = await workflow.ExecuteAsync(plan, confirmation, CancellationToken.None);
        var replay = await workflow.ExecuteAsync(plan, confirmation, CancellationToken.None);

        Assert.Equal(WorkloadInstallationExecutionStatus.Succeeded, result.Status);
        Assert.Equal(WorkloadInstallationExecutionStatus.ConfirmationRejected, replay.Status);
        Assert.Single(adapter.Requests);
        Assert.True(adapter.NetworkStarted);
        Assert.True(adapter.WorkspaceModified);
        Assert.Equal("wasm-tools", adapter.Requests[0].Id);
    }

    private static DependencyGuidance CreateWorkloadGuidance() =>
        new(
            Capability(DependencyKind.Workload, "wasm-tools", "工作負載不存在"),
            DependencyGuidanceKind.WorkloadInstallation,
            "需要安裝工作負載：wasm-tools",
            "工作負載尚未就緒，需獨立確認。",
            "https://learn.microsoft.com/dotnet/core/tools/dotnet-workload-install",
            requiresIndependentConfirmation: true);

    private static DependencyCapability Capability(
        DependencyKind kind,
        string id,
        string reason) =>
        new(
            new DependencyRequirement(kind, id, kind == DependencyKind.DotNetSdk ? "10.0" : null),
            EnvironmentDetectionStatus.Missing,
            null,
            "fixture",
            reason);

    private sealed class FixtureWorkloadInstallationAdapter : IWorkloadInstallationAdapter
    {
        public List<DependencyRequirement> Requests { get; } = [];
        public bool NetworkStarted { get; private set; }
        public bool WorkspaceModified { get; private set; }

        public Task<WorkloadInstallationAdapterResult> InstallAsync(
            DependencyRequirement requirement,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(requirement);
            NetworkStarted = true;
            WorkspaceModified = true;
            return Task.FromResult(new WorkloadInstallationAdapterResult(true, null));
        }
    }
}
