using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using DotNetScaffoldStudio.Application;
using DotNetScaffoldStudio.Domain;

namespace DotNetScaffoldStudio.Infrastructure;

public sealed class DependencyDiscoveryService(
    IDotNetEnvironmentDiscovery environmentDiscovery) : IDependencyDiscovery
{
    private static readonly Regex VersionPattern = new(
        @"^(?<major>\d+)(?:\.(?<minor>\d+))?(?:\.(?<patch>\d+))?(?:\.(?<revision>\d+))?(?<suffix>[-+].*)?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public async Task<DependencyDiscoveryResult> DiscoverAsync(
        string workspaceRoot,
        string? targetProjectPath,
        IReadOnlyList<DependencyRequirement> requirements,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentNullException.ThrowIfNull(requirements);

        var root = Path.GetFullPath(workspaceRoot);
        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException($"找不到工作區：{root}");
        }

        var normalizedRequirements = NormalizeRequirements(requirements);
        var environment = await environmentDiscovery.DiscoverAsync(root, cancellationToken);
        var packageResult = normalizedRequirements.Any(requirement => requirement.Kind == DependencyKind.NuGetPackage)
            ? await ReadProjectPackagesAsync(root, targetProjectPath, cancellationToken)
            : ProjectPackageResult.Empty;

        var capabilities = normalizedRequirements
            .Select(requirement => Detect(requirement, root, targetProjectPath, environment, packageResult))
            .ToArray();
        var diagnostics = capabilities
            .Where(capability => !capability.IsAvailable && capability.Reason is not null)
            .Select(capability => $"{capability.Requirement.Kind} {capability.Requirement.Id}：{capability.Reason}")
            .Concat(packageResult.Diagnostics)
            .ToArray();

        return new DependencyDiscoveryResult(GetOverallStatus(capabilities), capabilities, diagnostics);
    }

    private static IReadOnlyList<DependencyRequirement> NormalizeRequirements(
        IReadOnlyList<DependencyRequirement> requirements)
    {
        var normalized = new List<DependencyRequirement>();
        var indexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var requirement in requirements)
        {
            ArgumentNullException.ThrowIfNull(requirement);
            var key = $"{requirement.Kind}:{requirement.Id}";
            if (!indexes.TryGetValue(key, out var index))
            {
                indexes.Add(key, normalized.Count);
                normalized.Add(requirement);
                continue;
            }

            var existing = normalized[index];
            if (CompareMinimumVersions(requirement.MinimumVersion, existing.MinimumVersion) > 0)
            {
                normalized[index] = requirement;
            }
        }

        return normalized;
    }

    private static DependencyCapability Detect(
        DependencyRequirement requirement,
        string workspaceRoot,
        string? targetProjectPath,
        DotNetEnvironmentSnapshot environment,
        ProjectPackageResult packageResult) =>
        requirement.Kind switch
        {
            DependencyKind.LocalTool => DetectLocalTool(requirement, workspaceRoot, environment),
            DependencyKind.ToolManifest => DetectToolManifest(requirement, workspaceRoot, environment),
            DependencyKind.NuGetPackage => DetectPackage(requirement, targetProjectPath, packageResult),
            DependencyKind.DotNetSdk => DetectSdk(requirement, environment),
            DependencyKind.Workload => DetectWorkload(requirement, environment),
            _ => new DependencyCapability(
                requirement,
                EnvironmentDetectionStatus.DetectionFailed,
                null,
                null,
                "不支援此相依性類型的能力偵測。")
        };

    private static DependencyCapability DetectLocalTool(
        DependencyRequirement requirement,
        string workspaceRoot,
        DotNetEnvironmentSnapshot environment)
    {
        var source = Path.Combine(workspaceRoot, ".config", "dotnet-tools.json");
        if (environment.ToolManifestStatus == EnvironmentDetectionStatus.Missing)
        {
            return Missing(requirement, source, "工作區沒有本機工具資訊清單。");
        }

        if (environment.LocalToolStatus == EnvironmentDetectionStatus.DetectionFailed)
        {
            return Failed(requirement, source, "無法讀取或解析本機工具清單。");
        }

        var tool = environment.LocalTools.FirstOrDefault(candidate =>
            candidate.PackageId.Equals(requirement.Id, StringComparison.OrdinalIgnoreCase));
        return tool is null
            ? Missing(requirement, source, "本機工具清單中沒有此工具。")
            : EvaluateVersion(requirement, tool.Version, source);
    }

    private static DependencyCapability DetectToolManifest(
        DependencyRequirement requirement,
        string workspaceRoot,
        DotNetEnvironmentSnapshot environment)
    {
        var source = Path.Combine(workspaceRoot, ".config", "dotnet-tools.json");
        return environment.ToolManifestStatus switch
        {
            EnvironmentDetectionStatus.Available => new DependencyCapability(
                requirement,
                EnvironmentDetectionStatus.Available,
                null,
                source,
                null),
            EnvironmentDetectionStatus.Missing => Missing(requirement, source, "工作區沒有本機工具資訊清單。"),
            _ => Failed(requirement, source, "無法偵測本機工具資訊清單。")
        };
    }

    private static DependencyCapability DetectPackage(
        DependencyRequirement requirement,
        string? targetProjectPath,
        ProjectPackageResult packageResult)
    {
        if (packageResult.FailureReason is not null)
        {
            return Failed(requirement, targetProjectPath, packageResult.FailureReason);
        }

        var package = packageResult.Packages.FirstOrDefault(candidate =>
            candidate.Id.Equals(requirement.Id, StringComparison.OrdinalIgnoreCase));
        if (package is null)
        {
            return Missing(requirement, targetProjectPath, "目標專案沒有直接參考此 NuGet 套件。");
        }

        if (string.IsNullOrWhiteSpace(package.Version))
        {
            return Failed(requirement, targetProjectPath, "已找到套件參考，但無法解析其版本。");
        }

        return EvaluateVersion(requirement, package.Version, targetProjectPath);
    }

    private static DependencyCapability DetectSdk(
        DependencyRequirement requirement,
        DotNetEnvironmentSnapshot environment) =>
        environment.SdkStatus switch
        {
            EnvironmentDetectionStatus.Missing => Missing(requirement, "dotnet", "找不到相容的 .NET SDK。"),
            EnvironmentDetectionStatus.DetectionFailed => Failed(requirement, "dotnet", "無法偵測 .NET SDK。"),
            _ when string.IsNullOrWhiteSpace(environment.ActiveSdkVersion) =>
                Failed(requirement, "dotnet", "已找到 .NET SDK，但無法解析目前 SDK 版本。"),
            _ => EvaluateVersion(requirement, environment.ActiveSdkVersion!, "dotnet")
        };

    private static DependencyCapability DetectWorkload(
        DependencyRequirement requirement,
        DotNetEnvironmentSnapshot environment)
    {
        if (environment.WorkloadStatus == EnvironmentDetectionStatus.DetectionFailed)
        {
            return Failed(requirement, "dotnet workload list", "無法偵測工作負載清單。");
        }

        var workload = environment.Workloads.FirstOrDefault(candidate =>
            candidate.Id.Equals(requirement.Id, StringComparison.OrdinalIgnoreCase));
        if (workload is null)
        {
            return Missing(requirement, "dotnet workload list", "本機未安裝此工作負載。");
        }

        return string.IsNullOrWhiteSpace(requirement.MinimumVersion) ||
               string.IsNullOrWhiteSpace(workload.InstalledManifestVersion)
            ? new DependencyCapability(
                requirement,
                EnvironmentDetectionStatus.Available,
                workload.InstalledManifestVersion,
                "dotnet workload list",
                null)
            : EvaluateVersion(requirement, workload.InstalledManifestVersion, "dotnet workload list");
    }

    private static DependencyCapability EvaluateVersion(
        DependencyRequirement requirement,
        string detectedVersion,
        string? source)
    {
        if (string.IsNullOrWhiteSpace(requirement.MinimumVersion))
        {
            return new DependencyCapability(requirement, EnvironmentDetectionStatus.Available, detectedVersion, source, null);
        }

        if (!TryParseVersion(detectedVersion, out var detected) ||
            !TryParseVersion(requirement.MinimumVersion, out var minimum))
        {
            return Failed(requirement, source, $"版本格式無法解析（偵測到 {detectedVersion}，需求至少 {requirement.MinimumVersion}）。");
        }

        if (CompareVersions(detected, minimum) >= 0)
        {
            return new DependencyCapability(requirement, EnvironmentDetectionStatus.Available, detectedVersion, source, null);
        }

        var majorConflict = detected.Major != minimum.Major;
        var reason = majorConflict
            ? $"主要版本不相容：目前為 {detectedVersion}，需求為 {requirement.MinimumVersion} 以上。"
            : $"版本不足：目前為 {detectedVersion}，需求為 {requirement.MinimumVersion} 以上。";
        return new DependencyCapability(
            requirement,
            EnvironmentDetectionStatus.Incompatible,
            detectedVersion,
            source,
            reason);
    }

    private static DependencyCapability Missing(
        DependencyRequirement requirement,
        string? source,
        string reason) =>
        new(requirement, EnvironmentDetectionStatus.Missing, null, source, reason);

    private static DependencyCapability Failed(
        DependencyRequirement requirement,
        string? source,
        string reason) =>
        new(requirement, EnvironmentDetectionStatus.DetectionFailed, null, source, reason);

    private static EnvironmentDetectionStatus GetOverallStatus(IReadOnlyList<DependencyCapability> capabilities) =>
        capabilities.Any(capability => capability.Status == EnvironmentDetectionStatus.DetectionFailed)
            ? EnvironmentDetectionStatus.DetectionFailed
            : capabilities.Any(capability => capability.Status == EnvironmentDetectionStatus.Incompatible)
                ? EnvironmentDetectionStatus.Incompatible
                : capabilities.Any(capability => capability.Status == EnvironmentDetectionStatus.Missing)
                    ? EnvironmentDetectionStatus.Missing
                    : EnvironmentDetectionStatus.Available;

    private static async Task<ProjectPackageResult> ReadProjectPackagesAsync(
        string workspaceRoot,
        string? targetProjectPath,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(targetProjectPath))
        {
            return ProjectPackageResult.Failed("需要先選定目標 .csproj 才能偵測必要 NuGet 套件。");
        }

        var projectPath = Path.IsPathFullyQualified(targetProjectPath)
            ? Path.GetFullPath(targetProjectPath)
            : Path.GetFullPath(Path.Combine(workspaceRoot, targetProjectPath));
        if (!string.Equals(Path.GetExtension(projectPath), ".csproj", StringComparison.OrdinalIgnoreCase) ||
            !File.Exists(projectPath))
        {
            return ProjectPackageResult.Failed("目標 .csproj 不存在或副檔名不正確。");
        }

        if (!WorkspacePathResolver.IsPathWithin(workspaceRoot, projectPath))
        {
            return ProjectPackageResult.Failed("目標 .csproj 不在目前工作區內。");
        }

        try
        {
            await using var stream = File.OpenRead(projectPath);
            var document = await XDocument.LoadAsync(stream, LoadOptions.None, cancellationToken);
            var packages = ReadPackageReferences(document, projectPath);
            var centralVersions = await ReadCentralPackageVersionsAsync(workspaceRoot, projectPath, cancellationToken);
            var resolvedPackages = packages
                .Select(package => package with
                {
                    Version = package.Version ?? centralVersions.GetValueOrDefault(package.Id)
                })
                .ToArray();
            return new ProjectPackageResult(resolvedPackages, null, []);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (UnauthorizedAccessException exception)
        {
            return ProjectPackageResult.Failed($"無法讀取目標 .csproj：{exception.Message}");
        }
        catch (IOException exception)
        {
            return ProjectPackageResult.Failed($"讀取目標 .csproj 失敗：{exception.Message}");
        }
        catch (XmlException exception)
        {
            return ProjectPackageResult.Failed($"目標 .csproj XML 無法解析：{exception.Message}");
        }
    }

    private static IReadOnlyList<ProjectPackageReference> ReadPackageReferences(
        XDocument document,
        string projectPath) =>
        document
            .Descendants()
            .Where(element => element.Name.LocalName.Equals("PackageReference", StringComparison.Ordinal))
            .Select(element => new ProjectPackageReference(
                ReadAttributeOrElement(element, "Include") ?? ReadAttributeOrElement(element, "Update") ?? string.Empty,
                ReadAttributeOrElement(element, "Version") ?? ReadAttributeOrElement(element, "VersionOverride"),
                projectPath))
            .Where(package => package.Id.Length > 0)
            .ToArray();

    private static async Task<IReadOnlyDictionary<string, string>> ReadCentralPackageVersionsAsync(
        string workspaceRoot,
        string projectPath,
        CancellationToken cancellationToken)
    {
        var versions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var root = Path.GetFullPath(workspaceRoot);
        var directory = Path.GetDirectoryName(projectPath)!;
        while (WorkspacePathResolver.IsPathWithin(root, directory))
        {
            var propsPath = Path.Combine(directory, "Directory.Packages.props");
            if (File.Exists(propsPath))
            {
                await using var stream = File.OpenRead(propsPath);
                var document = await XDocument.LoadAsync(stream, LoadOptions.None, cancellationToken);
                foreach (var element in document.Descendants().Where(item =>
                             item.Name.LocalName.Equals("PackageVersion", StringComparison.Ordinal)))
                {
                    var id = ReadAttributeOrElement(element, "Include") ?? ReadAttributeOrElement(element, "Update");
                    var version = ReadAttributeOrElement(element, "Version");
                    if (!string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(version))
                    {
                        versions.TryAdd(id, version);
                    }
                }
            }

            if (string.Equals(directory, root, StringComparison.Ordinal))
            {
                break;
            }

            var parent = Directory.GetParent(directory)?.FullName;
            if (parent is null || string.Equals(parent, directory, StringComparison.Ordinal))
            {
                break;
            }

            directory = parent;
        }

        return versions;
    }

    private static string? ReadAttributeOrElement(XElement element, string name) =>
        element.Attribute(name)?.Value?.Trim() ??
        element.Elements().FirstOrDefault(child => child.Name.LocalName.Equals(name, StringComparison.Ordinal))?.Value.Trim();

    private static bool TryParseVersion(string value, out ParsedVersion version)
    {
        var match = VersionPattern.Match(value.Trim());
        if (!match.Success || !int.TryParse(match.Groups["major"].Value, out var major))
        {
            version = default;
            return false;
        }

        version = new ParsedVersion(
            major,
            ParsePart(match, "minor"),
            ParsePart(match, "patch"),
            ParsePart(match, "revision"),
            match.Groups["suffix"].Success && match.Groups["suffix"].Value.StartsWith("-", StringComparison.Ordinal));
        return true;
    }

    private static int ParsePart(Match match, string name) =>
        match.Groups[name].Success && int.TryParse(match.Groups[name].Value, out var value) ? value : 0;

    private static int CompareMinimumVersions(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left))
        {
            return string.IsNullOrWhiteSpace(right) ? 0 : -1;
        }

        if (string.IsNullOrWhiteSpace(right) ||
            !TryParseVersion(left, out var leftVersion) ||
            !TryParseVersion(right, out var rightVersion))
        {
            return 1;
        }

        return CompareVersions(leftVersion, rightVersion);
    }

    private static int CompareVersions(ParsedVersion left, ParsedVersion right)
    {
        var comparison = left.Major.CompareTo(right.Major);
        if (comparison != 0)
        {
            return comparison;
        }

        comparison = left.Minor.CompareTo(right.Minor);
        if (comparison != 0)
        {
            return comparison;
        }

        comparison = left.Patch.CompareTo(right.Patch);
        if (comparison != 0)
        {
            return comparison;
        }

        comparison = left.Revision.CompareTo(right.Revision);
        return comparison != 0
            ? comparison
            : left.IsPrerelease == right.IsPrerelease
                ? 0
                : left.IsPrerelease ? -1 : 1;
    }

    private readonly record struct ParsedVersion(
        int Major,
        int Minor,
        int Patch,
        int Revision,
        bool IsPrerelease);

    private sealed record ProjectPackageReference(string Id, string? Version, string Source);

    private sealed record ProjectPackageResult(
        IReadOnlyList<ProjectPackageReference> Packages,
        string? FailureReason,
        IReadOnlyList<string> Diagnostics)
    {
        public static ProjectPackageResult Empty { get; } = new([], null, []);

        public static ProjectPackageResult Failed(string reason) => new([], reason, [reason]);
    }
}
