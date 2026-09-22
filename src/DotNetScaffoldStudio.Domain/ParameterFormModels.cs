namespace DotNetScaffoldStudio.Domain;

public sealed record ParameterValue
{
    private ParameterValue(
        ParameterValueKind kind,
        bool? booleanValue,
        string? textValue,
        IReadOnlyList<string> listValue)
    {
        Kind = kind;
        BooleanValue = booleanValue;
        TextValue = textValue;
        ListValue = Array.AsReadOnly(listValue.ToArray());
    }

    public ParameterValueKind Kind { get; }
    public bool? BooleanValue { get; }
    public string? TextValue { get; }
    public IReadOnlyList<string> ListValue { get; }

    public static ParameterValue Empty(ParameterDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return definition.Kind switch
        {
            ParameterValueKind.Boolean => Boolean(false),
            ParameterValueKind.List => List([]),
            _ => Text(string.Empty, definition.Kind)
        };
    }

    public static ParameterValue Boolean(bool value) =>
        new(ParameterValueKind.Boolean, value, null, []);

    public static ParameterValue Enumeration(string value) =>
        Text(value, ParameterValueKind.Enumeration);

    public static ParameterValue Path(string value) =>
        Text(value, ParameterValueKind.Path);

    public static ParameterValue List(IReadOnlyList<string> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return new(ParameterValueKind.List, null, null, values);
    }

    public static ParameterValue Text(string value) =>
        Text(value, ParameterValueKind.Text);

    public static ParameterValue Secret(string value) =>
        Text(value, ParameterValueKind.Secret);

    private static ParameterValue Text(string value, ParameterValueKind kind)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new(kind, null, value, []);
    }
}

public sealed class ParameterFieldState
{
    public ParameterFieldState(ParameterDefinition definition)
    {
        Definition = definition;
        Value = ParameterValue.Empty(definition);
    }

    public ParameterDefinition Definition { get; }
    public ParameterValue Value { get; private set; }
    public bool IsAdvanced => Definition.IsAdvanced;
    public bool IsSecret => Definition.IsSecret;

    public void SetValue(ParameterValue value)
    {
        Value = value;
    }
}
