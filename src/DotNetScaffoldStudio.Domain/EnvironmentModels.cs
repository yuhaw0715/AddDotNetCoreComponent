namespace DotNetScaffoldStudio.Domain;

public enum EnvironmentDetectionStatus
{
    Available,
    Missing,
    Incompatible,
    DetectionFailed
}

public sealed record DotNetSdkInstallation(string Version, string BasePath);

public sealed record DotNetRuntimeInstallation(string Name, string Version, string BasePath);

public sealed record DotNetWorkloadInstallation(string Id, string? InstalledManifestVersion);

public sealed record LocalDotNetTool(string PackageId, string Version, string Commands);

public sealed record DotNetEnvironmentSnapshot(
    EnvironmentDetectionStatus DotNetStatus,
    string? HostVersion,
    string? HostArchitecture,
    string? ActiveSdkVersion,
    EnvironmentDetectionStatus SdkStatus,
    IReadOnlyList<DotNetSdkInstallation> Sdks,
    EnvironmentDetectionStatus RuntimeStatus,
    IReadOnlyList<DotNetRuntimeInstallation> Runtimes,
    EnvironmentDetectionStatus WorkloadStatus,
    IReadOnlyList<DotNetWorkloadInstallation> Workloads,
    EnvironmentDetectionStatus ToolManifestStatus,
    EnvironmentDetectionStatus LocalToolStatus,
    IReadOnlyList<LocalDotNetTool> LocalTools,
    IReadOnlyList<string> Diagnostics);

public sealed record DotNetTemplateDiscoveryResult
{
    public DotNetTemplateDiscoveryResult(
        EnvironmentDetectionStatus status,
        IReadOnlyList<LocalTemplateCapability> templates,
        IReadOnlyList<string> rawStandardOutput,
        IReadOnlyList<string> rawStandardError,
        IReadOnlyList<string> diagnostics)
    {
        Status = status;
        Templates = Array.AsReadOnly(templates.ToArray());
        RawStandardOutput = Array.AsReadOnly(rawStandardOutput.ToArray());
        RawStandardError = Array.AsReadOnly(rawStandardError.ToArray());
        Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
    }

    public EnvironmentDetectionStatus Status { get; }
    public IReadOnlyList<LocalTemplateCapability> Templates { get; }
    public IReadOnlyList<string> RawStandardOutput { get; }
    public IReadOnlyList<string> RawStandardError { get; }
    public IReadOnlyList<string> Diagnostics { get; }
}
