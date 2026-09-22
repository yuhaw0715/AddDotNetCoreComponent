using System.Collections.ObjectModel;

namespace DotNetScaffoldStudio.Domain;

public enum FeatureGroup
{
    ProjectAndService,
    Test,
    WindowsDesktop,
    Item,
    StructureAndConfiguration,
    AspNetScaffolding,
    EfCoreCodeGeneration,
    EfCoreMigration,
    EfCoreInspection,
    EfCoreDatabase,
    Custom
}

public enum ParameterValueKind
{
    Boolean,
    Enumeration,
    Path,
    List,
    Text,
    Secret
}

public enum PlatformFamily
{
    MacOS,
    Windows,
    Linux
}

public enum DependencyKind
{
    DotNetSdk,
    Workload,
    LocalTool,
    ToolManifest,
    NuGetPackage
}

public enum CatalogAvailability
{
    Available,
    Missing,
    VersionIncompatible,
    UnsupportedPlatform,
    UnknownLocalTemplate
}

public sealed record ParameterDefinition
{
    public ParameterDefinition(
        string id,
        string displayName,
        ParameterValueKind kind,
        bool isRequired = false,
        bool isAdvanced = false,
        IReadOnlyList<string>? allowedValues = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        var values = allowedValues?.ToArray() ?? [];
        if (kind == ParameterValueKind.Enumeration && values.Length == 0)
        {
            throw new ArgumentException("列舉參數必須至少包含一個允許值。", nameof(allowedValues));
        }

        if (kind != ParameterValueKind.Enumeration && values.Length > 0)
        {
            throw new ArgumentException("只有列舉參數可以定義允許值。", nameof(allowedValues));
        }

        if (values.Any(string.IsNullOrWhiteSpace) || values.Distinct(StringComparer.Ordinal).Count() != values.Length)
        {
            throw new ArgumentException("允許值不得空白或重複。", nameof(allowedValues));
        }

        Id = id;
        DisplayName = displayName;
        Kind = kind;
        IsRequired = isRequired;
        IsAdvanced = isAdvanced;
        AllowedValues = Array.AsReadOnly(values);
    }

    public string Id { get; }
    public string DisplayName { get; }
    public ParameterValueKind Kind { get; }
    public bool IsRequired { get; }
    public bool IsAdvanced { get; }
    public IReadOnlyList<string> AllowedValues { get; }
    public bool IsSecret => Kind == ParameterValueKind.Secret;
}

public sealed record DependencyRequirement
{
    public DependencyRequirement(DependencyKind kind, string id, string? minimumVersion = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        Kind = kind;
        Id = id;
        MinimumVersion = minimumVersion;
    }

    public DependencyKind Kind { get; }
    public string Id { get; }
    public string? MinimumVersion { get; }
}

public sealed record CatalogFeature
{
    public CatalogFeature(
        string id,
        string displayName,
        FeatureGroup group,
        string commandKind,
        IReadOnlyList<string> shortNames,
        FeatureRisk risk = FeatureRisk.Normal,
        IReadOnlySet<PlatformFamily>? supportedPlatforms = null,
        IReadOnlyList<ParameterDefinition>? parameters = null,
        IReadOnlyList<DependencyRequirement>? dependencies = null,
        IReadOnlyList<string>? variants = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(commandKind);

        var names = shortNames.ToArray();
        if (names.Length == 0 || names.Any(string.IsNullOrWhiteSpace) || names.Distinct(StringComparer.Ordinal).Count() != names.Length)
        {
            throw new ArgumentException("功能必須包含至少一個不重複的短名稱。", nameof(shortNames));
        }

        var parameterArray = parameters?.ToArray() ?? [];
        if (parameterArray.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() != parameterArray.Length)
        {
            throw new ArgumentException("參數識別字不得重複。", nameof(parameters));
        }

        Id = id;
        DisplayName = displayName;
        Group = group;
        CommandKind = commandKind;
        ShortNames = Array.AsReadOnly(names);
        Risk = risk;
        SupportedPlatforms = supportedPlatforms is null
            ? new ReadOnlySet<PlatformFamily>(new HashSet<PlatformFamily>(Enum.GetValues<PlatformFamily>()))
            : new ReadOnlySet<PlatformFamily>(new HashSet<PlatformFamily>(supportedPlatforms));
        Parameters = Array.AsReadOnly(parameterArray);
        Dependencies = Array.AsReadOnly(dependencies?.ToArray() ?? []);
        Variants = Array.AsReadOnly(variants?.ToArray() ?? []);
    }

    public string Id { get; }
    public string DisplayName { get; }
    public FeatureGroup Group { get; }
    public string CommandKind { get; }
    public IReadOnlyList<string> ShortNames { get; }
    public FeatureRisk Risk { get; }
    public IReadOnlySet<PlatformFamily> SupportedPlatforms { get; }
    public IReadOnlyList<ParameterDefinition> Parameters { get; }
    public IReadOnlyList<DependencyRequirement> Dependencies { get; }
    public IReadOnlyList<string> Variants { get; }
}

public sealed record LocalTemplateCapability(
    string DisplayName,
    IReadOnlyList<string> ShortNames,
    IReadOnlyList<string> Languages,
    IReadOnlyList<string> Tags,
    bool IsVersionCompatible = true);

public sealed record ResolvedCatalogFeature(
    CatalogFeature Feature,
    CatalogAvailability Availability,
    string? Reason,
    LocalTemplateCapability? LocalCapability = null);

public sealed record CommandResult
{
    public CommandResult(
        int exitCode,
        DateTimeOffset startedAt,
        DateTimeOffset completedAt,
        IReadOnlyList<string> standardOutput,
        IReadOnlyList<string> standardError,
        bool wasCancelled = false,
        bool gracefulTerminationAttempted = false,
        bool wasForceTerminated = false)
    {
        if (completedAt < startedAt)
        {
            throw new ArgumentException("完成時間不得早於開始時間。", nameof(completedAt));
        }

        if (gracefulTerminationAttempted && !wasCancelled)
        {
            throw new ArgumentException("只有取消中的命令才能嘗試正常終止。", nameof(gracefulTerminationAttempted));
        }

        if (wasForceTerminated && !gracefulTerminationAttempted)
        {
            throw new ArgumentException("強制終止前必須先嘗試正常終止。", nameof(wasForceTerminated));
        }

        ExitCode = exitCode;
        StartedAt = startedAt;
        CompletedAt = completedAt;
        StandardOutput = Array.AsReadOnly(standardOutput.ToArray());
        StandardError = Array.AsReadOnly(standardError.ToArray());
        WasCancelled = wasCancelled;
        GracefulTerminationAttempted = gracefulTerminationAttempted;
        WasForceTerminated = wasForceTerminated;
    }

    public int ExitCode { get; }
    public DateTimeOffset StartedAt { get; }
    public DateTimeOffset CompletedAt { get; }
    public IReadOnlyList<string> StandardOutput { get; }
    public IReadOnlyList<string> StandardError { get; }
    public bool WasCancelled { get; }
    public bool GracefulTerminationAttempted { get; }
    public bool WasForceTerminated { get; }
    public bool Succeeded => ExitCode == 0 && !WasCancelled;
}
