using DotNetScaffoldStudio.Application;
using DotNetScaffoldStudio.Domain;

namespace DotNetScaffoldStudio.UnitTests;

public sealed class DotNetTemplateListParserTests
{
    [Theory]
    [InlineData("dotnet-8-narrow.txt", "console", "acme-service")]
    [InlineData("dotnet-9-wide.txt", "webapi", "fabrikam-api")]
    [InlineData("dotnet-10-default.txt", "web", "contoso-service")]
    public void Parse_RecognizesOfficialAndCustomRowsAcrossSdkFormats(
        string fixtureName,
        string officialShortName,
        string customShortName)
    {
        var parsed = DotNetTemplateListParser.Parse(ReadFixture(fixtureName));

        Assert.True(parsed.IsValid);
        Assert.Contains(parsed.Templates, template => template.ShortNames.Contains(officialShortName));
        Assert.Contains(parsed.Templates, template => template.ShortNames.Contains(customShortName));
    }

    [Fact]
    public void Parse_PreservesAliasesLanguagesAndTags()
    {
        var parsed = DotNetTemplateListParser.Parse(ReadFixture("dotnet-10-default.txt"));

        var webApp = Assert.Single(parsed.Templates, template => template.ShortNames.Contains("webapp"));
        Assert.Equal(["webapp", "razor"], webApp.ShortNames);
        Assert.Equal(["C#"], webApp.Languages);
        Assert.Equal(["Web", "MVC", "Razor Pages"], webApp.Tags);

        var item = Assert.Single(parsed.Templates, template => template.ShortNames.Contains("gitattributes"));
        Assert.Empty(item.Languages);
        Assert.Equal(["Config"], item.Tags);
    }

    [Fact]
    public void Parse_MergesWithOfficialCatalogWithoutMisclassifyingCustomTemplate()
    {
        var parsed = DotNetTemplateListParser.Parse(ReadFixture("dotnet-10-default.txt"));

        var merged = FeatureCatalogMerger.MergeDotNetNew(
            OfficialFeatureCatalog.DotNetNew,
            parsed.Templates,
            PlatformFamily.MacOS);

        Assert.Equal(
            CatalogAvailability.Available,
            merged.Single(item => item.Feature.Id == "web").Availability);
        Assert.Equal(
            CatalogAvailability.UnknownLocalTemplate,
            merged.Single(item => item.Feature.Id == "custom:contoso-service").Availability);
    }

    [Fact]
    public void Parse_ReportsUnrecognizedOutputWithoutCreatingTemplateEntries()
    {
        var parsed = DotNetTemplateListParser.Parse(["unexpected output"]);

        Assert.False(parsed.IsValid);
        Assert.Empty(parsed.Templates);
        Assert.NotEmpty(parsed.Diagnostics);
    }

    private static IReadOnlyList<string> ReadFixture(string name)
    {
        var root = FindRepositoryRoot();
        return File.ReadAllLines(Path.Combine(
            root,
            "tests",
            "DotNetScaffoldStudio.UnitTests",
            "Fixtures",
            "dotnet-new-list",
            name));
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
