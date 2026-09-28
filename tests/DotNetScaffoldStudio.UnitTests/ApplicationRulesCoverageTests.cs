using DotNetScaffoldStudio.Application;
using DotNetScaffoldStudio.Domain;

namespace DotNetScaffoldStudio.UnitTests;

public sealed class ApplicationRulesCoverageTests
{
    private const string WorkspaceRoot = "/tmp/application-rules-coverage";

    [Fact]
    public void ParameterValidator_ValidStateHasNoErrors()
    {
        var feature = CreateValidationFeature();
        var state = new ParameterFormState(feature.Parameters);
        state.SetValue("name", ParameterValue.Text("Order"));
        state.SetValue("enabled", ParameterValue.Boolean(false));
        state.SetValue("output", ParameterValue.Path("Generated"));

        var result = new ParameterValidator().Validate(feature, state, WorkspaceRoot);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ParameterValidator_ReportsRequiredNamePathConditionalAndMutualExclusionErrors()
    {
        var feature = CreateValidationFeature();
        var state = new ParameterFormState(feature.Parameters);
        state.SetValue("name", ParameterValue.Text("invalid name"));
        state.SetValue("enabled", ParameterValue.Boolean(true));
        state.SetValue("first", ParameterValue.Text("one"));
        state.SetValue("second", ParameterValue.Text("two"));
        state.SetValue("output", ParameterValue.Path("../outside"));

        var result = new ParameterValidator().Validate(feature, state, WorkspaceRoot);

        Assert.Contains(result.Errors, error => error.Kind == ParameterValidationErrorKind.NameFormat);
        Assert.Contains(result.Errors, error => error.Kind == ParameterValidationErrorKind.PathBoundary);
        Assert.Contains(result.Errors, error => error.Kind == ParameterValidationErrorKind.ConditionalRequired);
        Assert.Equal(2, result.Errors.Count(error => error.Kind == ParameterValidationErrorKind.MutuallyExclusive));
    }

    [Fact]
    public void ParameterValidator_MissingSchemaFieldIsRejected()
    {
        var feature = new CatalogFeature(
            "required-feature",
            "Required feature",
            FeatureGroup.ProjectAndService,
            "dotnet-new",
            ["required"],
            parameters: [new ParameterDefinition("name", "名稱", ParameterValueKind.Text, isRequired: true)]);

        var result = new ParameterValidator().Validate(feature, new ParameterFormState([]), WorkspaceRoot);

        var error = Assert.Single(result.Errors);
        Assert.Equal(ParameterValidationErrorKind.Required, error.Kind);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void CommandRiskPolicy_NormalCommandDoesNotNeedConfirmation()
    {
        var request = new CommandRequest("dotnet", [new CommandArgument("new")], WorkspaceRoot, FeatureRisk.Normal, true);
        var policy = new CommandRiskPolicy();

        Assert.False(policy.RequiresConfirmation(request));
        Assert.True(policy.ValidateAndConsume(request, null));
        Assert.Throws<InvalidOperationException>(() => policy.Issue(request));
    }

    [Fact]
    public void SensitiveDataRedactor_ReplacesEveryMarkedSecret()
    {
        const string connection = "Server=db;Password=hidden";
        const string token = "token-value";
        var request = new CommandRequest(
            "dotnet",
            [new CommandArgument(connection, isSecret: true), new CommandArgument(token, isSecret: true)],
            WorkspaceRoot,
            FeatureRisk.DatabaseChange,
            true);

        var redacted = new SensitiveDataRedactor(request).Redact($"{connection} {token}");

        Assert.DoesNotContain(connection, redacted, StringComparison.Ordinal);
        Assert.DoesNotContain(token, redacted, StringComparison.Ordinal);
        Assert.Equal(2, redacted.Split(CommandPreviewFormatter.SecretMask, StringSplitOptions.None).Length - 1);
    }

    private static CatalogFeature CreateValidationFeature() =>
        new(
            "validation-feature",
            "Validation feature",
            FeatureGroup.ProjectAndService,
            "dotnet-new",
            ["validation"],
            parameters:
            [
                new ParameterDefinition("name", "名稱", ParameterValueKind.Text, isRequired: true),
                new ParameterDefinition("enabled", "啟用", ParameterValueKind.Boolean),
                new ParameterDefinition("dependent", "相依欄位", ParameterValueKind.Text),
                new ParameterDefinition("first", "第一欄", ParameterValueKind.Text),
                new ParameterDefinition("second", "第二欄", ParameterValueKind.Text),
                new ParameterDefinition("output", "輸出", ParameterValueKind.Path)
            ],
            constraints:
            [
                ParameterConstraint.NameFormat("name"),
                ParameterConstraint.PathWithinWorkspace("output"),
                ParameterConstraint.RequiredWhen("enabled", "dependent"),
                ParameterConstraint.MutuallyExclusive("first", "second")
            ]);
}
