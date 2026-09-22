using DotNetScaffoldStudio.Application;
using DotNetScaffoldStudio.Domain;

namespace DotNetScaffoldStudio.UnitTests;

public sealed class AspNetScaffoldingCommandFactoryTests
{
    private const string WorkspaceRoot = "/tmp/aspnet-scaffolding-factory-workspace";

    [Fact]
    public void AllOfficialGenerators_CreateStructuredCommandSnapshots()
    {
        var factory = new AspNetScaffoldingCommandFactory(new ParameterValidator());
        var selectedVariants = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["area"] = "Empty",
            ["controller"] = "RestApi",
            ["blazor"] = "CRUD",
            ["blazor-identity"] = "IdentityFiles",
            ["identity"] = "IdentityFiles",
            ["minimalapi"] = "CRUD",
            ["razorpage"] = "CRUD",
            ["view"] = "Empty"
        };

        var snapshots = new List<string>();
        foreach (var feature in OfficialFeatureCatalog.AspNetScaffolding)
        {
            var state = new ParameterFormState(feature.Parameters);
            state.SetValue("project", ParameterValue.Path("src/Api/Api.csproj"));
            state.SetValue("variant", ParameterValue.Enumeration(selectedVariants[feature.Id]));
            SetIfPresent(state, "name", ParameterValue.Text("Orders"));
            SetIfPresent(state, "model", ParameterValue.Text("Order"));
            SetIfPresent(state, "dataContext", ParameterValue.Text("CommerceDbContext"));
            SetIfPresent(state, "databaseProvider", ParameterValue.Enumeration("Sqlite"));
            SetIfPresent(state, "files", ParameterValue.List(["Account.Login", "Account.Register"]));
            state.SetValue("output", ParameterValue.Path($"Generated/{feature.Id}"));

            var request = factory.Create(feature, state, WorkspaceRoot);
            var arguments = request.Arguments.Select(argument => argument.Value).ToArray();
            snapshots.Add($"{feature.Id}|{string.Join(' ', arguments)}");

            Assert.Equal("dotnet", request.Executable);
            Assert.True(request.ModifiesWorkspace);
            Assert.Equal(WorkspaceRoot, request.WorkingDirectory);
            Assert.Equal(["aspnet-codegenerator", feature.ShortNames[0]], arguments[..2]);
            Assert.Equal("src/Api/Api.csproj", ValueAfter(arguments, "-p"));
            Assert.Equal($"Generated/{feature.Id}", ValueAfter(arguments, "--relativeFolderPath"));
            Assert.Equal([$"Generated/{feature.Id}"], request.ExpectedOutputs);
        }

        Assert.Equal(
        [
            "area|aspnet-codegenerator area -p src/Api/Api.csproj --name Orders --relativeFolderPath Generated/area",
            "controller|aspnet-codegenerator controller --restWithNoViews -p src/Api/Api.csproj --name Orders --model Order --dataContext CommerceDbContext --databaseProvider Sqlite --relativeFolderPath Generated/controller",
            "blazor|aspnet-codegenerator blazor CRUD -p src/Api/Api.csproj --name Orders --model Order --dataContext CommerceDbContext --databaseProvider Sqlite --relativeFolderPath Generated/blazor",
            "blazor-identity|aspnet-codegenerator blazor-identity -p src/Api/Api.csproj --dataContext CommerceDbContext --databaseProvider Sqlite --files Account.Login;Account.Register --relativeFolderPath Generated/blazor-identity",
            "identity|aspnet-codegenerator identity -p src/Api/Api.csproj --dataContext CommerceDbContext --databaseProvider Sqlite --files Account.Login;Account.Register --relativeFolderPath Generated/identity",
            "minimalapi|aspnet-codegenerator minimalapi CRUD -p src/Api/Api.csproj --name Orders --model Order --dataContext CommerceDbContext --databaseProvider Sqlite --relativeFolderPath Generated/minimalapi",
            "razorpage|aspnet-codegenerator razorpage CRUD -p src/Api/Api.csproj --name Orders --model Order --dataContext CommerceDbContext --databaseProvider Sqlite --relativeFolderPath Generated/razorpage",
            "view|aspnet-codegenerator view -p src/Api/Api.csproj --name Orders --relativeFolderPath Generated/view"
        ], snapshots);
    }

    [Fact]
    public void Create_WhenValidationFails_DoesNotCreateCommand()
    {
        var feature = OfficialFeatureCatalog.AspNetScaffolding.Single(item => item.Id == "controller");
        var state = new ParameterFormState(feature.Parameters);
        state.SetValue("project", ParameterValue.Path("src/Api/Api.csproj"));
        state.SetValue("variant", ParameterValue.Enumeration("RestApi"));
        state.SetValue("name", ParameterValue.Text("invalid name"));
        state.SetValue("output", ParameterValue.Path("../outside"));

        var exception = Assert.Throws<ParameterValidationException>(() =>
            new AspNetScaffoldingCommandFactory(new ParameterValidator()).Create(feature, state, WorkspaceRoot));

        Assert.Contains(exception.Result.Errors, error => error.ParameterId == "name");
        Assert.Contains(exception.Result.Errors, error => error.ParameterId == "output");
    }

    [Fact]
    public void Create_WhenFeatureIsNotScaffolding_RejectsIt()
    {
        var feature = OfficialFeatureCatalog.DotNetNew.Single(item => item.Id == "webapi");
        var state = new ParameterFormState(feature.Parameters);

        Assert.Throws<ArgumentException>(() =>
            new AspNetScaffoldingCommandFactory(new ParameterValidator()).Create(feature, state, WorkspaceRoot));
    }

    private static void SetIfPresent(ParameterFormState state, string parameterId, ParameterValue value)
    {
        if (state.Fields.Any(field => field.Definition.Id == parameterId))
        {
            state.SetValue(parameterId, value);
        }
    }

    private static string ValueAfter(IReadOnlyList<string> arguments, string option)
    {
        var index = Array.IndexOf(arguments.ToArray(), option);
        Assert.True(index >= 0 && index + 1 < arguments.Count, $"找不到參數：{option}");
        return arguments[index + 1];
    }
}
