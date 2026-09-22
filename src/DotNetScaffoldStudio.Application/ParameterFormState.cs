using DotNetScaffoldStudio.Domain;

namespace DotNetScaffoldStudio.Application;

public sealed class ParameterFormState
{
    private readonly IReadOnlyDictionary<string, ParameterFieldState> _fieldsById;

    public ParameterFormState(IReadOnlyList<ParameterDefinition> schema)
    {
        ArgumentNullException.ThrowIfNull(schema);
        if (schema.Count != schema.Select(field => field.Id).Distinct(StringComparer.Ordinal).Count())
        {
            throw new ArgumentException("表單 schema 的參數識別字不得重複。", nameof(schema));
        }

        var fields = schema.Select(definition => new ParameterFieldState(definition)).ToArray();
        Fields = Array.AsReadOnly(fields);
        _fieldsById = fields.ToDictionary(field => field.Definition.Id, StringComparer.Ordinal);
    }

    public IReadOnlyList<ParameterFieldState> Fields { get; }
    public bool ShowAdvanced { get; private set; }

    public IReadOnlyList<ParameterFieldState> VisibleFields =>
        Fields.Where(parameterField => ShowAdvanced || !parameterField.IsAdvanced).ToArray();

    public void SetShowAdvanced(bool showAdvanced) => ShowAdvanced = showAdvanced;

    public void SetValue(string parameterId, ParameterValue value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(parameterId);
        ArgumentNullException.ThrowIfNull(value);
        if (!_fieldsById.TryGetValue(parameterId, out var field))
        {
            throw new KeyNotFoundException($"找不到表單參數：{parameterId}");
        }

        ValidateValue(field.Definition, value);
        field.SetValue(value);
    }

    public ParameterValue GetValue(string parameterId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(parameterId);
        return _fieldsById.TryGetValue(parameterId, out var field)
            ? field.Value
            : throw new KeyNotFoundException($"找不到表單參數：{parameterId}");
    }

    private static void ValidateValue(ParameterDefinition definition, ParameterValue value)
    {
        if (definition.Kind != value.Kind)
        {
            throw new ArgumentException(
                $"參數 {definition.Id} 的值型別不符合 schema。",
                nameof(value));
        }

        if (definition.Kind == ParameterValueKind.Enumeration &&
            !definition.AllowedValues.Contains(value.TextValue, StringComparer.Ordinal))
        {
            throw new ArgumentException(
                $"參數 {definition.Id} 的值不在允許的列舉選項中。",
                nameof(value));
        }
    }
}
