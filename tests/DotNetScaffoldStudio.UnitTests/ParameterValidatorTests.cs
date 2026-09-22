using DotNetScaffoldStudio.Application;
using DotNetScaffoldStudio.Domain;

namespace DotNetScaffoldStudio.UnitTests;

public sealed class ParameterValidatorTests
{
    private const string WorkspaceRoot = "/tmp/scaffold-validation-workspace";

    [Fact]
    public void ControllerName_InvalidValue_ReportsFieldLevelChineseError()
    {
        var feature = OfficialFeatureCatalog.AspNetScaffolding.Single(item => item.Id == "controller");
        var state = new ParameterFormState(feature.Parameters);
        state.SetValue("name", ParameterValue.Text("123 invalid name"));

        var result = new ParameterValidator().Validate(feature, state, WorkspaceRoot);

        var error = Assert.Single(result.Errors, item => item.ParameterId == "name");
        Assert.Equal(ParameterValidationErrorKind.NameFormat, error.Kind);
        Assert.Contains("名稱格式無效", error.Message, StringComparison.Ordinal);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void RazorPageProjectOutsideWorkspace_ReportsPathBoundaryError()
    {
        var feature = OfficialFeatureCatalog.AspNetScaffolding.Single(item => item.Id == "razorpage");
        var state = new ParameterFormState(feature.Parameters);
        state.SetValue("project", ParameterValue.Path("../outside/Demo.csproj"));

        var result = new ParameterValidator().Validate(feature, state, WorkspaceRoot);

        var error = Assert.Single(result.Errors, item => item.ParameterId == "project");
        Assert.Equal(ParameterValidationErrorKind.PathBoundary, error.Kind);
        Assert.Contains("目前工作區內", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BlazorCrudWithoutName_ReportsConditionalRequiredError()
    {
        var feature = OfficialFeatureCatalog.AspNetScaffolding.Single(item => item.Id == "blazor");
        var state = new ParameterFormState(feature.Parameters);
        state.SetValue("project", ParameterValue.Path("src/Api/Demo.csproj"));
        state.SetValue("variant", ParameterValue.Enumeration("CRUD"));

        var result = new ParameterValidator().Validate(feature, state, WorkspaceRoot);

        var error = Assert.Single(result.Errors, item => item.ParameterId == "name");
        Assert.Equal(ParameterValidationErrorKind.ConditionalRequired, error.Kind);
        Assert.Contains("範本", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EfCoreContextAndConnectionString_ReportsMutualExclusionWithoutLeakingSecret()
    {
        var feature = OfficialFeatureCatalog.EfCore.Single(item => item.Id == "dbcontext-scaffold");
        var state = new ParameterFormState(feature.Parameters);
        state.SetValue("project", ParameterValue.Path("src/Api/Demo.csproj"));
        state.SetValue("context", ParameterValue.Text("AppDbContext"));
        state.SetValue("connection", ParameterValue.Secret("Server=secret.example;Password=hidden"));

        var result = new ParameterValidator().Validate(feature, state, WorkspaceRoot);

        Assert.Equal(2, result.Errors.Count(error => error.Kind == ParameterValidationErrorKind.MutuallyExclusive));
        Assert.DoesNotContain(
            result.Errors,
            error => error.Message.Contains("secret.example", StringComparison.Ordinal) ||
                     error.Message.Contains("hidden", StringComparison.Ordinal));
    }
}
