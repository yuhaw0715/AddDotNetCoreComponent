using DotNetScaffoldStudio.Domain;

namespace DotNetScaffoldStudio.Application;

public sealed class DependencyGuidanceService : IDependencyGuidanceService
{
    private const string DotNetSdkDocumentationUrl = "https://dotnet.microsoft.com/download/dotnet/10.0";
    private const string WorkloadDocumentationUrl = "https://learn.microsoft.com/dotnet/core/tools/dotnet-workload-install";

    public DependencyGuidanceResult Build(DependencyDiscoveryResult discovery)
    {
        ArgumentNullException.ThrowIfNull(discovery);

        var items = new List<DependencyGuidance>();
        var diagnostics = discovery.Diagnostics.ToList();
        foreach (var capability in discovery.Capabilities)
        {
            if (capability.Status is not (
                EnvironmentDetectionStatus.Missing or
                EnvironmentDetectionStatus.Incompatible or
                EnvironmentDetectionStatus.DetectionFailed))
            {
                continue;
            }

            switch (capability.Requirement.Kind)
            {
                case DependencyKind.DotNetSdk when capability.Status != EnvironmentDetectionStatus.DetectionFailed:
                    items.Add(new DependencyGuidance(
                        capability,
                        DependencyGuidanceKind.DotNetSdkDocumentation,
                        ".NET SDK 尚未就緒",
                        CreateSdkDescription(capability),
                        DotNetSdkDocumentationUrl,
                        requiresIndependentConfirmation: false));
                    break;
                case DependencyKind.Workload when capability.Status != EnvironmentDetectionStatus.DetectionFailed:
                    items.Add(new DependencyGuidance(
                        capability,
                        DependencyGuidanceKind.WorkloadInstallation,
                        $"需要安裝工作負載：{capability.Requirement.Id}",
                        CreateWorkloadDescription(capability),
                        WorkloadDocumentationUrl,
                        requiresIndependentConfirmation: true));
                    break;
                case DependencyKind.DotNetSdk or DependencyKind.Workload:
                    items.Add(new DependencyGuidance(
                        capability,
                        DependencyGuidanceKind.DetectionFailure,
                        $"無法確認 {capability.Requirement.Kind} 狀態",
                        capability.Reason ?? "請重新整理環境資訊後再試。",
                        null,
                        requiresIndependentConfirmation: false));
                    break;
            }
        }

        return new DependencyGuidanceResult(items, diagnostics);
    }

    private static string CreateSdkDescription(DependencyCapability capability)
    {
        var minimum = string.IsNullOrWhiteSpace(capability.Requirement.MinimumVersion)
            ? string.Empty
            : $"（需求至少 {capability.Requirement.MinimumVersion}）";
        return $"目前找不到可用的 .NET SDK{minimum}。請依官方文件安裝或修復 SDK；應用程式不會自行下載或執行 SDK 安裝器。";
    }

    private static string CreateWorkloadDescription(DependencyCapability capability) =>
        $"工作負載 {capability.Requirement.Id} 尚未就緒。安裝可能連線並修改 SDK 環境，必須在獨立確認後才可交給安裝 adapter。";
}

public sealed record WorkloadInstallationConfirmation(Guid Id, string PlanFingerprint);

public enum WorkloadInstallationExecutionStatus
{
    Succeeded,
    ConfirmationRejected,
    Failed
}

public sealed record WorkloadInstallationAdapterResult(bool Succeeded, string? Diagnostic);

public sealed record WorkloadInstallationExecutionResult(
    WorkloadInstallationExecutionStatus Status,
    WorkloadInstallationPlan Plan,
    WorkloadInstallationAdapterResult? AdapterResult,
    string? Diagnostic)
{
    public bool ConfirmationRejected => Status == WorkloadInstallationExecutionStatus.ConfirmationRejected;
}

public sealed class WorkloadInstallationConfirmationPolicy
{
    private readonly HashSet<Guid> _activeTokens = [];

    public WorkloadInstallationConfirmation Issue(WorkloadInstallationPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var token = new WorkloadInstallationConfirmation(Guid.NewGuid(), GetFingerprint(plan));
        _activeTokens.Add(token.Id);
        return token;
    }

    public bool ValidateAndConsume(
        WorkloadInstallationPlan plan,
        WorkloadInstallationConfirmation? confirmation)
    {
        ArgumentNullException.ThrowIfNull(plan);
        return confirmation is not null &&
               string.Equals(confirmation.PlanFingerprint, GetFingerprint(plan), StringComparison.Ordinal) &&
               _activeTokens.Remove(confirmation.Id);
    }

    private static string GetFingerprint(WorkloadInstallationPlan plan) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(string.Join(
                '\u001f',
                plan.Requirement.Kind,
                plan.Requirement.Id,
                plan.Requirement.MinimumVersion ?? string.Empty,
                plan.Guidance.Description))));
}

public sealed class WorkloadInstallationWorkflow(
    IWorkloadInstallationAdapter adapter,
    WorkloadInstallationConfirmationPolicy confirmationPolicy) : IWorkloadInstallationWorkflow
{
    public WorkloadInstallationPlan CreatePlan(DependencyGuidance guidance) => new(guidance);

    public async Task<WorkloadInstallationExecutionResult> ExecuteAsync(
        WorkloadInstallationPlan plan,
        WorkloadInstallationConfirmation? confirmation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (!confirmationPolicy.ValidateAndConsume(plan, confirmation))
        {
            return new WorkloadInstallationExecutionResult(
                WorkloadInstallationExecutionStatus.ConfirmationRejected,
                plan,
                null,
                "工作負載安裝尚未取得獨立確認，未啟動任何網路或修改操作。");
        }

        var adapterResult = await adapter.InstallAsync(plan.Requirement, cancellationToken);
        return new WorkloadInstallationExecutionResult(
            adapterResult.Succeeded
                ? WorkloadInstallationExecutionStatus.Succeeded
                : WorkloadInstallationExecutionStatus.Failed,
            plan,
            adapterResult,
            adapterResult.Succeeded ? null : adapterResult.Diagnostic ?? "工作負載安裝失敗。");
    }
}
