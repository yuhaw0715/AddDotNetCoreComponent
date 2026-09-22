using DotNetScaffoldStudio.Application;
using DotNetScaffoldStudio.Domain;
using DotNetScaffoldStudio.Infrastructure;

namespace DotNetScaffoldStudio.IntegrationTests;

public sealed class DependencyDiscoveryServiceTests
{
    [Fact]
    public async Task DiscoverAsync_WhenToolsAndCentralPackagesMatch_ReportsAvailableCapabilities()
    {
        using var workspace = TemporaryDirectory.Create();
        var projectPath = await WriteProjectAsync(workspace.Path, "src/Demo.Api/Demo.Api.csproj", includeVersions: false);
        await File.WriteAllTextAsync(
            Path.Combine(workspace.Path, "Directory.Packages.props"),
            """
            <Project>
              <ItemGroup>
                <PackageVersion Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.2" />
                <PackageVersion Include="Microsoft.VisualStudio.Web.CodeGeneration.Design" Version="10.0.1" />
              </ItemGroup>
            </Project>
            """);

        var service = new DependencyDiscoveryService(new FixtureEnvironmentDiscovery(CreateEnvironment(
            [
                new LocalDotNetTool("dotnet-ef", "10.0.0", "dotnet-ef"),
                new LocalDotNetTool("dotnet-aspnet-codegenerator", "10.0.1", "dotnet-aspnet-codegenerator")
            ])));
        var requirements = Requirements();

        var result = await service.DiscoverAsync(workspace.Path, projectPath, requirements, CancellationToken.None);

        Assert.Equal(EnvironmentDetectionStatus.Available, result.Status);
        Assert.True(result.IsFullyAvailable);
        Assert.All(result.Capabilities, capability => Assert.True(capability.IsAvailable));
        Assert.Equal("10.0.2", Find(result, "Microsoft.EntityFrameworkCore.Design").DetectedVersion);
        Assert.Equal("10.0.1", Find(result, "Microsoft.VisualStudio.Web.CodeGeneration.Design").DetectedVersion);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public async Task DiscoverAsync_WhenToolAndPackageMajorVersionsConflict_ReportsIncompatible()
    {
        using var workspace = TemporaryDirectory.Create();
        var projectPath = await WriteProjectAsync(workspace.Path, "Demo.Api.csproj", includeVersions: true, version: "9.0.0");

        var service = new DependencyDiscoveryService(new FixtureEnvironmentDiscovery(CreateEnvironment(
            [
                new LocalDotNetTool("dotnet-ef", "9.0.0", "dotnet-ef"),
                new LocalDotNetTool("dotnet-aspnet-codegenerator", "10.0.0", "dotnet-aspnet-codegenerator")
            ])));

        var result = await service.DiscoverAsync(workspace.Path, projectPath, Requirements(), CancellationToken.None);

        Assert.Equal(EnvironmentDetectionStatus.Incompatible, result.Status);
        Assert.Equal(EnvironmentDetectionStatus.Incompatible, Find(result, "dotnet-ef").Status);
        Assert.Equal(EnvironmentDetectionStatus.Incompatible, Find(result, "Microsoft.EntityFrameworkCore.Design").Status);
        Assert.Contains(result.Diagnostics, message => message.Contains("主要版本不相容", StringComparison.Ordinal));
        Assert.False(result.IsFullyAvailable);
    }

    [Fact]
    public async Task DiscoverAsync_WhenToolManifestOrPackageIsMissing_ReportsMissingWithoutInstallation()
    {
        using var workspace = TemporaryDirectory.Create();
        var projectPath = await WriteProjectAsync(workspace.Path, "Demo.Api.csproj", includeVersions: false, includePackages: false);
        var environment = CreateEnvironment([], toolManifestStatus: EnvironmentDetectionStatus.Missing, localToolStatus: EnvironmentDetectionStatus.Missing);
        var service = new DependencyDiscoveryService(new FixtureEnvironmentDiscovery(environment));

        var result = await service.DiscoverAsync(workspace.Path, projectPath, Requirements(), CancellationToken.None);

        Assert.Equal(EnvironmentDetectionStatus.Missing, result.Status);
        Assert.All(result.Capabilities, capability => Assert.Equal(EnvironmentDetectionStatus.Missing, capability.Status));
        Assert.Contains(result.Diagnostics, message => message.Contains("工具資訊清單", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Diagnostics, message => message.Contains("安裝", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DiscoverAsync_WhenTargetProjectIsOutsideWorkspace_ReportsDetectionFailure()
    {
        using var workspace = TemporaryDirectory.Create();
        using var outside = TemporaryDirectory.Create();
        var projectPath = await WriteProjectAsync(outside.Path, "Outside.csproj", includeVersions: true, version: "10.0.0");
        var service = new DependencyDiscoveryService(new FixtureEnvironmentDiscovery(CreateEnvironment(
            [new LocalDotNetTool("dotnet-ef", "10.0.0", "dotnet-ef")])));

        var result = await service.DiscoverAsync(
            workspace.Path,
            projectPath,
            [new DependencyRequirement(DependencyKind.NuGetPackage, "Microsoft.EntityFrameworkCore.Design", "10.0")],
            CancellationToken.None);

        var capability = Assert.Single(result.Capabilities);
        Assert.Equal(EnvironmentDetectionStatus.DetectionFailed, capability.Status);
        Assert.Contains("不在目前工作區", capability.Reason, StringComparison.Ordinal);
    }

    private static IReadOnlyList<DependencyRequirement> Requirements() =>
    [
        new(DependencyKind.LocalTool, "dotnet-ef", "10.0"),
        new(DependencyKind.LocalTool, "dotnet-aspnet-codegenerator", "10.0"),
        new(DependencyKind.NuGetPackage, "Microsoft.EntityFrameworkCore.Design", "10.0"),
        new(DependencyKind.NuGetPackage, "Microsoft.VisualStudio.Web.CodeGeneration.Design", "10.0")
    ];

    private static DependencyCapability Find(DependencyDiscoveryResult result, string id) =>
        Assert.Single(result.Capabilities, capability =>
            capability.Requirement.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    private static DotNetEnvironmentSnapshot CreateEnvironment(
        IReadOnlyList<LocalDotNetTool> tools,
        EnvironmentDetectionStatus toolManifestStatus = EnvironmentDetectionStatus.Available,
        EnvironmentDetectionStatus localToolStatus = EnvironmentDetectionStatus.Available) =>
        new(
            EnvironmentDetectionStatus.Available,
            "10.0.10",
            "arm64",
            "10.0.100",
            EnvironmentDetectionStatus.Available,
            [new DotNetSdkInstallation("10.0.100", "/dotnet/sdk")],
            EnvironmentDetectionStatus.Available,
            [new DotNetRuntimeInstallation("Microsoft.NETCore.App", "10.0.10", "/dotnet/shared")],
            EnvironmentDetectionStatus.Available,
            [],
            toolManifestStatus,
            localToolStatus,
            tools,
            []);

    private static async Task<string> WriteProjectAsync(
        string root,
        string relativePath,
        bool includeVersions,
        string version = "10.0.0",
        bool includePackages = true)
    {
        var path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var packageVersion = includeVersions ? $" Version=\"{version}\"" : string.Empty;
        var packageReferences = includePackages
            ? $"""
              <ItemGroup>
                <PackageReference Include="Microsoft.EntityFrameworkCore.Design"{packageVersion} />
                <PackageReference Include="Microsoft.VisualStudio.Web.CodeGeneration.Design"{packageVersion} />
              </ItemGroup>
              """
            : string.Empty;
        await File.WriteAllTextAsync(
            path,
            $"""
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
              {packageReferences}
            </Project>
            """);
        return path;
    }

    private sealed class FixtureEnvironmentDiscovery(DotNetEnvironmentSnapshot snapshot) : IDotNetEnvironmentDiscovery
    {
        public Task<DotNetEnvironmentSnapshot> DiscoverAsync(string workspaceRoot, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(snapshot);
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        private TemporaryDirectory(string path) => Path = path;

        public string Path { get; }

        public static TemporaryDirectory Create()
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"dotnet-scaffold-studio-dependencies-{Guid.NewGuid():N}");
            Directory.CreateDirectory(path);
            return new TemporaryDirectory(path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
