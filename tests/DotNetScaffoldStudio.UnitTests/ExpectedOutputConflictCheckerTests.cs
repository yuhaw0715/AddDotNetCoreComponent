using DotNetScaffoldStudio.Application;
using DotNetScaffoldStudio.Domain;

namespace DotNetScaffoldStudio.UnitTests;

public sealed class ExpectedOutputConflictCheckerTests
{
    private const string WorkspaceRoot = "/tmp/output-conflict-workspace";

    [Fact]
    public void ExistingControllerFile_BlocksExecutionWithoutForce()
    {
        var feature = OfficialFeatureCatalog.AspNetScaffolding.Single(item => item.Id == "controller");
        var state = new ParameterFormState(feature.Parameters);
        state.SetValue("project", ParameterValue.Path("src/Api/Api.csproj"));
        state.SetValue("variant", ParameterValue.Enumeration("Empty"));
        state.SetValue("name", ParameterValue.Text("OrdersController"));
        state.SetValue("output", ParameterValue.Path("Controllers"));
        var request = new AspNetScaffoldingCommandFactory(new ParameterValidator()).Create(feature, state, WorkspaceRoot);

        var result = new ExpectedOutputConflictChecker().Check(
            request,
            ["Controllers", "Controllers/OrdersController.cs"]);

        Assert.Equal(OutputConflictStatus.Blocked, result.Status);
        Assert.False(result.CanExecute);
        Assert.False(result.RequiresConfirmation);
        Assert.Equal(["Controllers", "Controllers/OrdersController.cs"], result.ConflictingPaths);
    }

    [Fact]
    public void ExistingDotNetNewOutputDirectory_BlocksExecutionByDefault()
    {
        var feature = OfficialFeatureCatalog.DotNetNew.Single(item => item.Id == "webapi");
        var state = new ParameterFormState(feature.Parameters);
        state.SetValue("name", ParameterValue.Text("DemoApi"));
        state.SetValue("output", ParameterValue.Path("generated/DemoApi"));
        var request = new DotNetNewCommandFactory(new ParameterValidator()).Create(feature, state, WorkspaceRoot);

        var result = new ExpectedOutputConflictChecker().Check(request, ["generated/DemoApi"]);

        Assert.Equal(OutputConflictStatus.Blocked, result.Status);
        Assert.Contains("已存在", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ForceChangesRiskAndRequiresExplicitOverwriteConfirmation()
    {
        var feature = OfficialFeatureCatalog.AspNetScaffolding.Single(item => item.Id == "controller");
        var state = new ParameterFormState(feature.Parameters);
        state.SetValue("project", ParameterValue.Path("src/Api/Api.csproj"));
        state.SetValue("variant", ParameterValue.Enumeration("Empty"));
        state.SetValue("name", ParameterValue.Text("OrdersController"));
        state.SetValue("output", ParameterValue.Path("Controllers"));
        state.SetValue("force", ParameterValue.Boolean(true));
        var request = new AspNetScaffoldingCommandFactory(new ParameterValidator()).Create(feature, state, WorkspaceRoot);

        var result = new ExpectedOutputConflictChecker().Check(request, ["Controllers/OrdersController.cs"]);

        Assert.Contains("--force", request.Arguments.Select(argument => argument.Value));
        Assert.Equal(FeatureRisk.FileOverwrite, request.Risk);
        Assert.Equal(OutputConflictStatus.RequiresConfirmation, result.Status);
        Assert.True(result.CanExecute);
        Assert.True(result.RequiresConfirmation);
    }

    [Fact]
    public void NoConflict_AllowsNormalExecution()
    {
        var request = new CommandRequest(
            "dotnet",
            [new CommandArgument("new"), new CommandArgument("webapi")],
            WorkspaceRoot,
            FeatureRisk.Normal,
            modifiesWorkspace: true,
            expectedOutputs: ["generated/DemoApi"]);

        var result = new ExpectedOutputConflictChecker().Check(request, []);

        Assert.Equal(OutputConflictStatus.NoConflict, result.Status);
        Assert.True(result.CanExecute);
        Assert.False(result.RequiresConfirmation);
    }
}
