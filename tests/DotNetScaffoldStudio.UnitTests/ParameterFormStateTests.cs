using DotNetScaffoldStudio.Application;
using DotNetScaffoldStudio.Domain;

namespace DotNetScaffoldStudio.UnitTests;

public sealed class ParameterFormStateTests
{
    [Fact]
    public void ToggleAdvancedFields_PreservesEveryTypedValue()
    {
        var state = new ParameterFormState(
        [
            new ParameterDefinition("enabled", "啟用", ParameterValueKind.Boolean),
            new ParameterDefinition("framework", "框架", ParameterValueKind.Enumeration, allowedValues: ["net10.0", "net9.0"]),
            new ParameterDefinition("output", "輸出", ParameterValueKind.Path, isAdvanced: true),
            new ParameterDefinition("tags", "標籤", ParameterValueKind.List, isAdvanced: true),
            new ParameterDefinition("name", "名稱", ParameterValueKind.Text),
            new ParameterDefinition("connection", "連線", ParameterValueKind.Secret, isAdvanced: true)
        ]);

        state.SetValue("enabled", ParameterValue.Boolean(true));
        state.SetValue("framework", ParameterValue.Enumeration("net10.0"));
        state.SetValue("output", ParameterValue.Path("src/Api"));
        state.SetValue("tags", ParameterValue.List(["api", "v1"]));
        state.SetValue("name", ParameterValue.Text("Orders"));
        state.SetValue("connection", ParameterValue.Secret("Name=ConnectionStrings:App"));

        Assert.False(state.ShowAdvanced);
        Assert.Equal(["enabled", "framework", "name"], state.VisibleFields.Select(field => field.Definition.Id));

        state.SetShowAdvanced(true);
        Assert.Equal(6, state.VisibleFields.Count);
        Assert.True(state.GetValue("enabled").BooleanValue);
        Assert.Equal("net10.0", state.GetValue("framework").TextValue);
        Assert.Equal("src/Api", state.GetValue("output").TextValue);
        Assert.Equal(["api", "v1"], state.GetValue("tags").ListValue);
        Assert.Equal("Orders", state.GetValue("name").TextValue);
        Assert.Equal("Name=ConnectionStrings:App", state.GetValue("connection").TextValue);

        state.SetShowAdvanced(false);
        Assert.Equal(3, state.VisibleFields.Count);
        Assert.Equal("src/Api", state.GetValue("output").TextValue);
        Assert.Equal(["api", "v1"], state.GetValue("tags").ListValue);
    }

    [Fact]
    public void SetValue_RejectsWrongTypeAndUnknownEnumeration()
    {
        var state = new ParameterFormState(
        [new ParameterDefinition("framework", "框架", ParameterValueKind.Enumeration, allowedValues: ["net10.0"])]);

        Assert.Throws<ArgumentException>(() => state.SetValue("framework", ParameterValue.Text("net10.0")));
        Assert.Throws<ArgumentException>(() => state.SetValue("framework", ParameterValue.Enumeration("net8.0")));
        Assert.Throws<KeyNotFoundException>(() => state.GetValue("missing"));
    }
}
