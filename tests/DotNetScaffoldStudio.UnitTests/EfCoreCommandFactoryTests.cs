using DotNetScaffoldStudio.Application;
using DotNetScaffoldStudio.Domain;

namespace DotNetScaffoldStudio.UnitTests;

public sealed class EfCoreCommandFactoryTests
{
    private const string WorkspaceRoot = "/tmp/ef-command-factory-workspace";

    [Fact]
    public void AllOfficialFeatures_CreateStructuredCommandSnapshots()
    {
        var factory = new EfCoreCommandFactory(new ParameterValidator());
        var snapshots = new List<string>();
        foreach (var feature in OfficialFeatureCatalog.EfCore)
        {
            var state = CreateState(feature);
            var request = factory.Create(feature, state, WorkspaceRoot);
            var arguments = request.Arguments.Select(argument => argument.Value).ToArray();
            snapshots.Add($"{feature.Id}|{string.Join(' ', arguments)}");

            Assert.Equal("dotnet", request.Executable);
            Assert.Equal(WorkspaceRoot, request.WorkingDirectory);
            string[] commandPrefix = ["ef", .. feature.ShortNames[0].Split(' ')];
            Assert.Equal(commandPrefix, arguments[..commandPrefix.Length]);
            Assert.Equal("src/Api/Demo.csproj", ValueAfter(arguments, "--project"));
            Assert.DoesNotContain("drop", arguments);
        }

        Assert.Equal(
        [
            "dbcontext-scaffold|ef dbcontext scaffold Name=ConnectionStrings:Commerce Microsoft.EntityFrameworkCore.Sqlite --project src/Api/Demo.csproj --output-dir Generated/dbcontext-scaffold",
            "dbcontext-optimize|ef dbcontext optimize --project src/Api/Demo.csproj --context CommerceDbContext --output-dir Generated/dbcontext-optimize",
            "dbcontext-script|ef dbcontext script --project src/Api/Demo.csproj --context CommerceDbContext --from Baseline --to InitialCreate --output Generated/dbcontext-script",
            "migrations-add|ef migrations add InitialCreate --project src/Api/Demo.csproj --context CommerceDbContext",
            "migrations-remove|ef migrations remove InitialCreate --project src/Api/Demo.csproj --context CommerceDbContext --force",
            "migrations-bundle|ef migrations bundle --project src/Api/Demo.csproj --context CommerceDbContext --output Generated/migrations-bundle",
            "migrations-list|ef migrations list --project src/Api/Demo.csproj --context CommerceDbContext",
            "migrations-has-pending-model-changes|ef migrations has-pending-model-changes --project src/Api/Demo.csproj --context CommerceDbContext",
            "migrations-script|ef migrations script --project src/Api/Demo.csproj --context CommerceDbContext --from Baseline --to InitialCreate --output Generated/migrations-script",
            "dbcontext-info|ef dbcontext info --project src/Api/Demo.csproj --context CommerceDbContext",
            "dbcontext-list|ef dbcontext list --project src/Api/Demo.csproj --context CommerceDbContext",
            "database-update|ef database update --project src/Api/Demo.csproj --connection Name=ConnectionStrings:Commerce"
        ], snapshots);
    }

    [Fact]
    public void DatabaseUpdate_CreatesConfirmationDataWithoutLeakingConnectionValue()
    {
        const string namedConnection = "Name=ConnectionStrings:Commerce";
        var feature = OfficialFeatureCatalog.EfCore.Single(item => item.Id == "database-update");
        var state = CreateState(feature);
        state.SetValue("connection", ParameterValue.Secret(namedConnection));
        var factory = new EfCoreCommandFactory(new ParameterValidator());
        var request = factory.Create(feature, state, WorkspaceRoot);
        var confirmation = factory.CreateDatabaseUpdateConfirmation(feature, state, request);

        Assert.Equal(FeatureRisk.DatabaseChange, request.Risk);
        Assert.Contains(request.Arguments, argument => argument.Value == namedConnection && argument.IsSecret);
        Assert.Equal(DatabaseConnectionSource.NamedConnection, confirmation.ConnectionSource);
        Assert.Equal("src/Api/Demo.csproj", confirmation.TargetProjectPath);
        Assert.Contains("••••••", confirmation.MaskedCommandPreview, StringComparison.Ordinal);
        Assert.DoesNotContain(namedConnection, confirmation.MaskedCommandPreview, StringComparison.Ordinal);
        Assert.Contains("二次確認", confirmation.RiskReason, StringComparison.Ordinal);

        var policy = new CommandRiskPolicy();
        var token = policy.Issue(request);
        Assert.True(policy.ValidateAndConsume(request, token));
        Assert.False(policy.ValidateAndConsume(request, token));
    }

    [Fact]
    public void DatabaseDrop_IsNotBuildable()
    {
        var feature = new CatalogFeature(
            "database-drop",
            "禁止的資料庫刪除",
            FeatureGroup.EfCoreDatabase,
            "dotnet-ef",
            ["database drop"],
            parameters:
            [
                new ParameterDefinition("project", "目標專案", ParameterValueKind.Path, isRequired: true)
            ]);
        var state = new ParameterFormState(feature.Parameters);
        state.SetValue("project", ParameterValue.Path("src/Api/Demo.csproj"));

        Assert.Throws<ArgumentException>(() =>
            new EfCoreCommandFactory(new ParameterValidator()).Create(feature, state, WorkspaceRoot));
        Assert.DoesNotContain(OfficialFeatureCatalog.EfCore, item => item.ShortNames.Contains("database drop"));
    }

    [Fact]
    public void Create_WhenProjectIsOutsideWorkspace_RejectsCommand()
    {
        var feature = OfficialFeatureCatalog.EfCore.Single(item => item.Id == "migrations-add");
        var state = CreateState(feature);
        state.SetValue("project", ParameterValue.Path("../outside/Demo.csproj"));

        var exception = Assert.Throws<ParameterValidationException>(() =>
            new EfCoreCommandFactory(new ParameterValidator()).Create(feature, state, WorkspaceRoot));

        Assert.Contains(exception.Result.Errors, error => error.ParameterId == "project");
    }

    private static ParameterFormState CreateState(CatalogFeature feature)
    {
        var state = new ParameterFormState(feature.Parameters);
        state.SetValue("project", ParameterValue.Path("src/Api/Demo.csproj"));
        SetIfPresent(state, "context", ParameterValue.Text("CommerceDbContext"));
        SetIfPresent(state, "provider", ParameterValue.Text("Microsoft.EntityFrameworkCore.Sqlite"));
        SetIfPresent(state, "migrationName", ParameterValue.Text("InitialCreate"));
        SetIfPresent(state, "fromMigration", ParameterValue.Text("Baseline"));
        SetIfPresent(state, "toMigration", ParameterValue.Text("InitialCreate"));
        SetIfPresent(state, "output", ParameterValue.Path($"Generated/{feature.Id}"));
        SetIfPresent(state, "migration", ParameterValue.Text(string.Empty));
        SetIfPresent(state, "force", ParameterValue.Boolean(feature.Id == "migrations-remove"));
        if (feature.Id == "dbcontext-scaffold")
        {
            state.SetValue("context", ParameterValue.Text(string.Empty));
            state.SetValue("connection", ParameterValue.Secret("Name=ConnectionStrings:Commerce"));
        }

        if (feature.Id == "database-update")
        {
            state.SetValue("context", ParameterValue.Text(string.Empty));
            state.SetValue("connection", ParameterValue.Secret("Name=ConnectionStrings:Commerce"));
        }

        return state;
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
