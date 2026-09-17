using DotNetScaffoldStudio.Domain;

namespace DotNetScaffoldStudio.UnitTests;

public sealed class CommandPreviewTests
{
    [Fact]
    public void DisplayText_QuotesValuesWithoutInterpretingShellCharacters()
    {
        var preview = new CommandPreview(
            "dotnet",
            ["new", "apicontroller", "--name", "Orders; rm -rf demo"],
            "/tmp/demo",
            FeatureRisk.Normal);

        Assert.Equal("dotnet new apicontroller --name \"Orders; rm -rf demo\"", preview.DisplayText);
        Assert.Equal("Orders; rm -rf demo", preview.Arguments[3]);
    }
}
