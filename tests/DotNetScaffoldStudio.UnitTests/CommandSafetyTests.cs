using DotNetScaffoldStudio.Application;
using DotNetScaffoldStudio.Domain;

namespace DotNetScaffoldStudio.UnitTests;

public sealed class CommandSafetyTests
{
    [Theory]
    [InlineData("含 空白")]
    [InlineData("含\"引號")]
    [InlineData("繁體中文")]
    [InlineData("a; `touch nope` | $(whoami)")]
    public void CommandRequest_PreservesEveryArgumentAsOneValue(string value)
    {
        var request = CreateRequest(FeatureRisk.Normal, new CommandArgument(value));

        Assert.Single(request.Arguments);
        Assert.Equal(value, request.Arguments[0].Value);
        Assert.Contains(value.Replace("\"", "\\\"", StringComparison.Ordinal), CommandPreviewFormatter.Format(request), StringComparison.Ordinal);
    }

    [Fact]
    public void CommandRequest_RejectsRelativeWorkingDirectory()
    {
        Assert.Throws<ArgumentException>(() =>
            new CommandRequest("dotnet", [], "relative/path", FeatureRisk.Normal, true));
    }

    [Fact]
    public void Redactor_RemovesSecretFromPreviewOutputHistoryAndErrors()
    {
        const string secret = "Server=db;Password=p@ss;Token=abc";
        var request = CreateRequest(FeatureRisk.Normal, new CommandArgument(secret, isSecret: true));
        var redactor = new SensitiveDataRedactor(request);
        var samples = new[]
        {
            CommandPreviewFormatter.Format(request),
            redactor.Redact($"stdout: {secret}"),
            redactor.Redact($"stderr: {secret}"),
            redactor.Redact($"history: {secret}"),
            redactor.Redact($"exception: {secret}")
        };

        Assert.All(samples, sample => Assert.DoesNotContain(secret, sample, StringComparison.Ordinal));
        Assert.All(samples, sample => Assert.Contains(CommandPreviewFormatter.SecretMask, sample, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(FeatureRisk.FileOverwrite)]
    [InlineData(FeatureRisk.FileRemoval)]
    [InlineData(FeatureRisk.DatabaseChange)]
    [InlineData(FeatureRisk.EnvironmentChange)]
    public void RiskPolicy_RequiresConfirmationForEveryRiskyOperation(FeatureRisk risk)
    {
        var policy = new CommandRiskPolicy();
        Assert.True(policy.RequiresConfirmation(CreateRequest(risk, new CommandArgument("value"))));
    }

    [Fact]
    public void ConfirmationToken_IsSingleUseAndInvalidAfterParametersChange()
    {
        var policy = new CommandRiskPolicy();
        var original = CreateRequest(FeatureRisk.DatabaseChange, new CommandArgument("Latest"));
        var changed = CreateRequest(FeatureRisk.DatabaseChange, new CommandArgument("PreviousMigration"));
        var changedToken = policy.Issue(original);

        Assert.False(policy.ValidateAndConsume(changed, changedToken));
        Assert.True(policy.ValidateAndConsume(original, changedToken));
        Assert.False(policy.ValidateAndConsume(original, changedToken));
    }

    private static CommandRequest CreateRequest(FeatureRisk risk, params CommandArgument[] arguments) =>
        new("dotnet", arguments, "/tmp/work space", risk, modifiesWorkspace: true);
}
