namespace DotNetScaffoldStudio.UnitTests;

public sealed class ArchitectureTests
{
    [Fact]
    public void Domain_HasNoProjectReferences()
    {
        var repositoryRoot = FindRepositoryRoot();
        var project = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "DotNetScaffoldStudio.Domain",
            "DotNetScaffoldStudio.Domain.csproj"));

        Assert.DoesNotContain("ProjectReference", project, StringComparison.Ordinal);
        Assert.DoesNotContain("PackageReference", project, StringComparison.Ordinal);
    }

    [Fact]
    public void Application_OnlyReferencesDomainProject()
    {
        var repositoryRoot = FindRepositoryRoot();
        var project = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "DotNetScaffoldStudio.Application",
            "DotNetScaffoldStudio.Application.csproj"));

        Assert.Contains("DotNetScaffoldStudio.Domain", project, StringComparison.Ordinal);
        Assert.DoesNotContain("DotNetScaffoldStudio.Infrastructure", project, StringComparison.Ordinal);
        Assert.DoesNotContain("DotNetScaffoldStudio.App", project, StringComparison.Ordinal);
        Assert.DoesNotContain("Avalonia", project, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "DotNetScaffoldStudio.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("找不到儲存庫根目錄。");
    }
}
