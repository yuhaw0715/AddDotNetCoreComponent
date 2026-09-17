using DotNetScaffoldStudio.Domain;
using DotNetScaffoldStudio.Infrastructure;

namespace DotNetScaffoldStudio.IntegrationTests;

public sealed class DemoExecutionServiceTests
{
    [Fact]
    public async Task ExecuteAsync_UsesControllerNameFromStructuredArgumentsInResult()
    {
        var service = new DemoExecutionService();
        var command = new CommandPreview(
            "dotnet",
            ["aspnet-codegenerator", "controller", "--controllerName", "InvoicesController", "--relativeFolderPath", "Controllers"],
            "/tmp/demo",
            FeatureRisk.Normal);

        var result = await service.ExecuteAsync(command, new Progress<string>(), CancellationToken.None);

        Assert.Contains("A  Controllers/InvoicesController.cs", result.FileChanges);
    }
}
