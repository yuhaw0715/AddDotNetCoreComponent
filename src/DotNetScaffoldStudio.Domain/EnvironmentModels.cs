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
