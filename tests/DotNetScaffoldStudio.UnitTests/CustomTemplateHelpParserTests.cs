using DotNetScaffoldStudio.Application;
using DotNetScaffoldStudio.Domain;

namespace DotNetScaffoldStudio.UnitTests;

public sealed class CustomTemplateHelpParserTests
{
    [Fact]
    public void Parse_ParseableHelp_ReturnsTypedOptionsAndChoices()
    {
        var result = CustomTemplateHelpParser.Parse(ReadFixture("parseable.txt"));

        Assert.Equal(CustomTemplateHelpParseMode.Parsed, result.Mode);
        Assert.Empty(result.Diagnostics);

        var framework = Assert.Single(result.Options, option => option.Name == "--framework");
        Assert.Equal(ParameterValueKind.Enumeration, framework.Kind);
        Assert.Equal(["net10.0", "net9.0"], framework.AllowedValues);
        Assert.Equal("net10.0", framework.DefaultValue);

        var enabled = Assert.Single(result.Options, option => option.Name == "--enabled");
        Assert.Equal(ParameterValueKind.Boolean, enabled.Kind);
        Assert.Equal("false", enabled.DefaultValue);

        var root = Assert.Single(result.Options, option => option.Name == "--root");
        Assert.Equal(ParameterValueKind.Path, root.Kind);
    }

    [Fact]
    public void Parse_PartialHelp_PreservesKnownOptionsAndRequestsRestrictedFallback()
    {
        var result = CustomTemplateHelpParser.Parse(ReadFixture("partial.txt"));

        Assert.Equal(CustomTemplateHelpParseMode.PartiallyParsed, result.Mode);
        Assert.Contains(result.Options, option => option.Name == "--known");
        Assert.Contains(result.Options, option => option.Name == "--mystery");
        Assert.NotEmpty(result.Diagnostics);
    }

    [Fact]
    public void Parse_UnparseableHelp_UsesSafeFallbackWithoutOptions()
    {
        var result = CustomTemplateHelpParser.Parse(ReadFixture("unparseable.txt"));

        Assert.Equal(CustomTemplateHelpParseMode.SafeFallback, result.Mode);
        Assert.Empty(result.Options);
        Assert.NotEmpty(result.Diagnostics);
    }

    [Fact]
    public void StructuredTemplateArgument_DoesNotReparseShellCharacters()
    {
        var value = "$(touch should-not-run); `echo unsafe` | cat";
        var argument = new StructuredTemplateArgument("--value", value);

        Assert.Equal(["--value", value], argument.ToCommandArguments().Select(item => item.Value));
    }

    private static IReadOnlyList<string> ReadFixture(string name)
    {
        var root = FindRepositoryRoot();
        return File.ReadAllLines(Path.Combine(
            root,
            "tests",
            "DotNetScaffoldStudio.UnitTests",
            "Fixtures",
            "dotnet-template-help",
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
