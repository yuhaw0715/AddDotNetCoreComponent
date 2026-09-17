using DotNetScaffoldStudio.Domain;
using DotNetScaffoldStudio.Infrastructure;

namespace DotNetScaffoldStudio.IntegrationTests;

public sealed class WorkspaceServiceTests
{
    [Fact]
    public async Task ScanProjectsAsync_FindsProjectsAndSkipsBuildOutput()
    {
        var root = Path.Combine(Path.GetTempPath(), $"dotnet-scaffold-studio-{Guid.NewGuid():N}");
        var projectDirectory = Path.Combine(root, "src", "Demo.Api");
        var ignoredDirectory = Path.Combine(root, "src", "Demo.Api", "obj", "Ignored");

        try
        {
            Directory.CreateDirectory(projectDirectory);
            Directory.CreateDirectory(ignoredDirectory);
            await File.WriteAllTextAsync(
                Path.Combine(projectDirectory, "Demo.Api.csproj"),
                "<Project Sdk=\"Microsoft.NET.Sdk.Web\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
            await File.WriteAllTextAsync(
                Path.Combine(ignoredDirectory, "Ignored.csproj"),
                "<Project Sdk=\"Microsoft.NET.Sdk\" />");

            var projects = await new WorkspaceService().ScanProjectsAsync(root, CancellationToken.None);

            var project = Assert.Single(projects);
            Assert.Equal("Demo.Api", project.Name);
            Assert.Equal("net10.0", project.TargetFramework);
            Assert.Equal("Microsoft.NET.Sdk.Web", project.Sdk);
            Assert.Equal(Path.Combine("src", "Demo.Api", "Demo.Api.csproj"), project.RelativePath);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    [Fact]
    public async Task ScanProjectsAsync_FindsMultipleProjectsAndSkipsHiddenAndLinkedDirectories()
    {
        using var workspace = TemporaryDirectory.Create();
        await WriteProjectAsync(workspace.Path, "src/Api/Api.csproj", "Microsoft.NET.Sdk.Web", "net10.0;net9.0", usePluralFrameworks: true);
        await WriteProjectAsync(workspace.Path, "tests/Tests/Tests.csproj", "Microsoft.NET.Sdk", "net10.0");
        await WriteProjectAsync(workspace.Path, ".hidden/Hidden.csproj", "Microsoft.NET.Sdk", "net10.0");

        var projects = await new WorkspaceService().ScanProjectsAsync(workspace.Path, CancellationToken.None);

        Assert.Equal(2, projects.Count);
        Assert.Equal("net10.0;net9.0", projects.Single(project => project.Name == "Api").TargetFramework);
        Assert.DoesNotContain(projects, project => project.Name == "Hidden");
    }

    [Theory]
    [InlineData("Demo.sln", WorkspaceSelectionKind.Solution)]
    [InlineData("Demo.slnx", WorkspaceSelectionKind.SolutionXml)]
    public async Task Resolve_AcceptsSolutionFiles(string fileName, WorkspaceSelectionKind expectedKind)
    {
        using var workspace = TemporaryDirectory.Create();
        var path = Path.Combine(workspace.Path, fileName);
        await File.WriteAllTextAsync(path, string.Empty);

        var selection = new WorkspacePathResolver().Resolve(path);

        Assert.Equal(expectedKind, selection.Kind);
        Assert.Equal(workspace.Path, selection.RootPath);
    }

    [Fact]
    public void Resolve_RejectsMissingAndUnsupportedFiles()
    {
        using var workspace = TemporaryDirectory.Create();
        var textFile = Path.Combine(workspace.Path, "readme.txt");
        File.WriteAllText(textFile, "demo");

        Assert.Throws<DirectoryNotFoundException>(() =>
            new WorkspacePathResolver().Resolve(Path.Combine(workspace.Path, "missing")));
        Assert.Throws<ArgumentException>(() => new WorkspacePathResolver().Resolve(textFile));
    }

    [Fact]
    public void Resolve_RejectsUnreadableDirectoryOnUnix()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var workspace = TemporaryDirectory.Create();
        var originalMode = File.GetUnixFileMode(workspace.Path);
        try
        {
            File.SetUnixFileMode(workspace.Path, UnixFileMode.None);
            Assert.Throws<UnauthorizedAccessException>(() => new WorkspacePathResolver().Resolve(workspace.Path));
        }
        finally
        {
            File.SetUnixFileMode(workspace.Path, originalMode);
        }
    }

    [Fact]
    public async Task TargetValidator_RejectsOutsideSymlinkAndDeletedProject()
    {
        using var workspace = TemporaryDirectory.Create();
        using var outside = TemporaryDirectory.Create();
        var outsideProject = await WriteProjectAsync(outside.Path, "Outside.csproj", "Microsoft.NET.Sdk", "net10.0");
        var linkedProject = Path.Combine(workspace.Path, "Linked.csproj");
        File.CreateSymbolicLink(linkedProject, outsideProject);
        var validator = new TargetProjectValidator();

        var outsideResult = validator.Validate(workspace.Path, linkedProject, workspace.Path);
        File.Delete(linkedProject);
        var deletedResult = validator.Validate(workspace.Path, linkedProject, workspace.Path);

        Assert.False(outsideResult.IsValid);
        Assert.Contains("不在目前工作區", outsideResult.ErrorMessage, StringComparison.Ordinal);
        Assert.False(deletedResult.IsValid);
        Assert.Contains("移動或刪除", deletedResult.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TargetValidator_RejectsMismatchedWorkingDirectory()
    {
        using var workspace = TemporaryDirectory.Create();
        using var other = TemporaryDirectory.Create();
        var project = await WriteProjectAsync(workspace.Path, "Demo.csproj", "Microsoft.NET.Sdk", "net10.0");

        var result = new TargetProjectValidator().Validate(workspace.Path, project, other.Path);

        Assert.False(result.IsValid);
        Assert.Contains("工作目錄", result.ErrorMessage, StringComparison.Ordinal);
    }

    private static async Task<string> WriteProjectAsync(
        string root,
        string relativePath,
        string sdk,
        string frameworks,
        bool usePluralFrameworks = false)
    {
        var path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var property = usePluralFrameworks ? "TargetFrameworks" : "TargetFramework";
        await File.WriteAllTextAsync(
            path,
            $"<Project Sdk=\"{sdk}\"><PropertyGroup><{property}>{frameworks}</{property}></PropertyGroup></Project>");
        return path;
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        private TemporaryDirectory(string path) => Path = path;
        public string Path { get; }

        public static TemporaryDirectory Create()
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"dotnet-scaffold-studio-{Guid.NewGuid():N}");
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
