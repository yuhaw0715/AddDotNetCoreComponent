using DotNetScaffoldStudio.Application;
using DotNetScaffoldStudio.Domain;

namespace DotNetScaffoldStudio.UnitTests;

public sealed class DotNetNewCommandFactoryTests
{
    private const string WorkspaceRoot = "/tmp/dotnet-new-factory-workspace";

    [Fact]
    public void AllOfficialTemplates_CreateStructuredCommandsWithTargetOutput()
    {
        var factory = new DotNetNewCommandFactory(new ParameterValidator());

        Assert.Equal(46, OfficialFeatureCatalog.DotNetNew.Count);
        foreach (var feature in OfficialFeatureCatalog.DotNetNew)
        {
            var state = new ParameterFormState(feature.Parameters);
            state.SetValue("name", ParameterValue.Text($"Demo_{feature.Id.Replace("-", "_", StringComparison.Ordinal)}"));
            state.SetValue("output", ParameterValue.Path($"generated/{feature.Id}"));

            var request = factory.Create(feature, state, WorkspaceRoot);
            var arguments = request.Arguments.Select(argument => argument.Value).ToArray();

            Assert.Equal("dotnet", request.Executable);
            Assert.Equal(WorkspaceRoot, request.WorkingDirectory);
            Assert.True(request.ModifiesWorkspace);
            Assert.Equal(["new", feature.ShortNames[0]], arguments[..2]);
            Assert.Contains("--name", arguments);
            Assert.Contains($"Demo_{feature.Id.Replace("-", "_", StringComparison.Ordinal)}", arguments);
            Assert.Contains("--output", arguments);
            Assert.Contains($"generated/{feature.Id}", arguments);
            Assert.Equal([$"generated/{feature.Id}"], request.ExpectedOutputs);
        }
    }

    [Fact]
    public void Create_WhenParameterValidationFails_DoesNotCreateCommand()
    {
        var feature = OfficialFeatureCatalog.DotNetNew.Single(item => item.Id == "webapi");
        var state = new ParameterFormState(feature.Parameters);
        state.SetValue("name", ParameterValue.Text("invalid name"));
        state.SetValue("output", ParameterValue.Path("../outside"));
        var factory = new DotNetNewCommandFactory(new ParameterValidator());

        var exception = Assert.Throws<ParameterValidationException>(() =>
            factory.Create(feature, state, WorkspaceRoot));

        Assert.Contains(exception.Result.Errors, error => error.ParameterId == "name");
        Assert.Contains(exception.Result.Errors, error => error.ParameterId == "output");
    }
}
