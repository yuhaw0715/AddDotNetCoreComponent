namespace DotNetScaffoldStudio.Domain;

public enum DependencyGuidanceKind
{
    DotNetSdkDocumentation,
    WorkloadInstallation,
    DetectionFailure
}

public sealed record DependencyGuidance
{
    public DependencyGuidance(
        DependencyCapability capability,
        DependencyGuidanceKind kind,
        string title,
        string description,
        string? documentationUrl,
        bool requiresIndependentConfirmation)
    {
        ArgumentNullException.ThrowIfNull(capability);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        Capability = capability;
        Kind = kind;
        Title = title;
        Description = description;
        DocumentationUrl = string.IsNullOrWhiteSpace(documentationUrl) ? null : documentationUrl;
        RequiresIndependentConfirmation = requiresIndependentConfirmation;
    }

    public DependencyCapability Capability { get; }
    public DependencyGuidanceKind Kind { get; }
    public string Title { get; }
    public string Description { get; }
    public string? DocumentationUrl { get; }
    public bool RequiresIndependentConfirmation { get; }
}

public sealed record DependencyGuidanceResult
{
    public DependencyGuidanceResult(
        IReadOnlyList<DependencyGuidance> items,
        IReadOnlyList<string> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(diagnostics);

        Items = Array.AsReadOnly(items.ToArray());
        Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
    }

    public IReadOnlyList<DependencyGuidance> Items { get; }
    public IReadOnlyList<string> Diagnostics { get; }
    public bool IsRestricted => Items.Count > 0;
}

public sealed record WorkloadInstallationPlan
{
    public WorkloadInstallationPlan(DependencyGuidance guidance)
    {
        ArgumentNullException.ThrowIfNull(guidance);
        if (guidance.Kind != DependencyGuidanceKind.WorkloadInstallation ||
            !guidance.RequiresIndependentConfirmation ||
            guidance.Capability.Requirement.Kind != DependencyKind.Workload)
        {
            throw new ArgumentException("只有需要獨立確認的工作負載說明才能建立安裝計畫。", nameof(guidance));
        }

        Guidance = guidance;
    }

    public DependencyGuidance Guidance { get; }
    public DependencyRequirement Requirement => Guidance.Capability.Requirement;
}
