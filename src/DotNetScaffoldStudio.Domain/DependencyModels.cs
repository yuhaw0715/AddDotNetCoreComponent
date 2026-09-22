namespace DotNetScaffoldStudio.Domain;

public sealed record DependencyCapability
{
    public DependencyCapability(
        DependencyRequirement requirement,
        EnvironmentDetectionStatus status,
        string? detectedVersion,
        string? source,
        string? reason)
    {
        ArgumentNullException.ThrowIfNull(requirement);

        Requirement = requirement;
        Status = status;
        DetectedVersion = string.IsNullOrWhiteSpace(detectedVersion) ? null : detectedVersion;
        Source = string.IsNullOrWhiteSpace(source) ? null : source;
        Reason = string.IsNullOrWhiteSpace(reason) ? null : reason;
    }

    public DependencyRequirement Requirement { get; }
    public EnvironmentDetectionStatus Status { get; }
    public string? DetectedVersion { get; }
    public string? Source { get; }
    public string? Reason { get; }
    public bool IsAvailable => Status == EnvironmentDetectionStatus.Available;
}

public sealed record DependencyDiscoveryResult
{
    public DependencyDiscoveryResult(
        EnvironmentDetectionStatus status,
        IReadOnlyList<DependencyCapability> capabilities,
        IReadOnlyList<string> diagnostics)
    {
        Status = status;
        Capabilities = Array.AsReadOnly(capabilities.ToArray());
        Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
    }

    public EnvironmentDetectionStatus Status { get; }
    public IReadOnlyList<DependencyCapability> Capabilities { get; }
    public IReadOnlyList<string> Diagnostics { get; }
    public bool IsFullyAvailable => Status == EnvironmentDetectionStatus.Available;
}
