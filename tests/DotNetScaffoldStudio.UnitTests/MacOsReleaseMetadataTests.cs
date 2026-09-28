using System.Xml.Linq;

namespace DotNetScaffoldStudio.UnitTests;

public sealed class MacOsReleaseMetadataTests
{
    [Theory]
    [InlineData("osx-arm64")]
    [InlineData("osx-x64")]
    public void PublishProfile_DeclaresSelfContainedRidAndVersion(string runtimeIdentifier)
    {
        var profilePath = RepositoryPath(
            "src",
            "DotNetScaffoldStudio.App",
            "Properties",
            "PublishProfiles",
            $"{runtimeIdentifier}.pubxml");
        var document = XDocument.Load(profilePath);
        var propertyGroup = document.Root?.Element("PropertyGroup");

        Assert.NotNull(propertyGroup);
        Assert.Equal(runtimeIdentifier, propertyGroup!.Element("RuntimeIdentifier")?.Value);
        Assert.Equal("true", propertyGroup.Element("SelfContained")?.Value, ignoreCase: true);
        Assert.Equal("0.1.0-demo", propertyGroup.Element("MacOsReleaseVersion")?.Value);
        Assert.Equal("false", propertyGroup.Element("PublishSingleFile")?.Value, ignoreCase: true);
        Assert.Equal("false", propertyGroup.Element("PublishTrimmed")?.Value, ignoreCase: true);
    }

    [Fact]
    public void BundleTarget_DeclaresUnsignedDevelopmentMetadata()
    {
        var targetPath = RepositoryPath("packaging", "macos", "SelfContainedMacOsApp.targets");
        var target = File.ReadAllText(targetPath);

        Assert.Contains("DotNetScaffoldStudio.Release.json", target, StringComparison.Ordinal);
        Assert.Contains("&quot;signed&quot;: false", target, StringComparison.Ordinal);
        Assert.Contains("&quot;codeSignature&quot;: &quot;adhoc (not Developer ID)&quot;", target, StringComparison.Ordinal);
        Assert.Contains("&quot;developerIdSigned&quot;: false", target, StringComparison.Ordinal);
        Assert.Contains("&quot;notarized&quot;: false", target, StringComparison.Ordinal);
        Assert.Contains("&quot;distribution&quot;: &quot;development&quot;", target, StringComparison.Ordinal);
        Assert.Contains("$(RuntimeIdentifier)", target, StringComparison.Ordinal);
    }

    private static string RepositoryPath(params string[] segments)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine([directory.FullName, .. segments]);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException($"找不到測試來源：{Path.Combine(segments)}");
    }
}
