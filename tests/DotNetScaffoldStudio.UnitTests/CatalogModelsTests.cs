using DotNetScaffoldStudio.Domain;

namespace DotNetScaffoldStudio.UnitTests;

public sealed class CatalogModelsTests
{
    [Fact]
    public void ParameterDefinition_EnumerationRequiresAllowedValues()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new ParameterDefinition("provider", "資料庫提供者", ParameterValueKind.Enumeration));

        Assert.Contains("列舉參數", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ParameterDefinition_SecretIsDerivedFromKind()
    {
        var parameter = new ParameterDefinition("connection", "連線字串", ParameterValueKind.Secret, isRequired: true);

        Assert.True(parameter.IsSecret);
        Assert.True(parameter.IsRequired);
    }

    [Fact]
    public void CatalogFeature_RejectsDuplicateParameters()
    {
        var parameters = new[]
        {
            new ParameterDefinition("name", "名稱", ParameterValueKind.Text),
            new ParameterDefinition("name", "名稱", ParameterValueKind.Text)
        };

        Assert.Throws<ArgumentException>(() =>
            new CatalogFeature("sample", "範例", FeatureGroup.Item, "dotnet-new", ["sample"], parameters: parameters));
    }

    [Fact]
    public void CommandResult_RejectsCompletionBeforeStart()
    {
        var start = DateTimeOffset.UtcNow;

        Assert.Throws<ArgumentException>(() =>
            new CommandResult(0, start, start.AddSeconds(-1), [], []));
    }
}
