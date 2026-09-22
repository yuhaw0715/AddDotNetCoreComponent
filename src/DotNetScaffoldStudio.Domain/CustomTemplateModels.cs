namespace DotNetScaffoldStudio.Domain;

public enum CustomTemplateHelpParseMode
{
    Parsed,
    PartiallyParsed,
    SafeFallback
}

public sealed record CustomTemplateHelpOption
{
    public CustomTemplateHelpOption(
        string name,
        string? alias,
        ParameterValueKind kind,
        string? valuePlaceholder,
        string description,
        bool isRequired = false,
        IReadOnlyList<string>? allowedValues = null,
        string? defaultValue = null)
    {
        if (!IsSafeOptionName(name))
        {
            throw new ArgumentException("範本選項名稱格式無效。", nameof(name));
        }

        if (alias is not null && !IsSafeOptionName(alias))
        {
            throw new ArgumentException("範本選項別名格式無效。", nameof(alias));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Name = name;
        Alias = alias;
        Kind = kind;
        ValuePlaceholder = valuePlaceholder;
        Description = description;
        IsRequired = isRequired;
        AllowedValues = Array.AsReadOnly(allowedValues?.ToArray() ?? []);
        DefaultValue = defaultValue;
    }

    public string Name { get; }
    public string? Alias { get; }
    public ParameterValueKind Kind { get; }
    public string? ValuePlaceholder { get; }
    public string Description { get; }
    public bool IsRequired { get; }
    public IReadOnlyList<string> AllowedValues { get; }
    public string? DefaultValue { get; }

    private static bool IsSafeOptionName(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || (value.StartsWith("--", StringComparison.Ordinal)
            ? value.Length <= 2
            : value.Length <= 1 || !value.StartsWith("-", StringComparison.Ordinal)))
        {
            return false;
        }

        var start = value.StartsWith("--", StringComparison.Ordinal) ? 2 : 1;
        return value[start..].All(character => char.IsLetterOrDigit(character) || character is '-' or '_' or '.');
    }
}

public sealed record StructuredTemplateArgument
{
    public StructuredTemplateArgument(string optionName, string? value)
    {
        if (!IsSafeOptionName(optionName))
        {
            throw new ArgumentException("範本選項名稱格式無效。", nameof(optionName));
        }

        if (value?.Contains('\0') == true)
        {
            throw new ArgumentException("範本選項值不得包含 NUL 字元。", nameof(value));
        }

        OptionName = optionName;
        Value = value;
    }

    public string OptionName { get; }
    public string? Value { get; }

    public IReadOnlyList<CommandArgument> ToCommandArguments()
    {
        var arguments = new List<CommandArgument> { new(OptionName) };
        if (Value is not null)
        {
            arguments.Add(new CommandArgument(Value));
        }

        return arguments.AsReadOnly();
    }

    private static bool IsSafeOptionName(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || (value.StartsWith("--", StringComparison.Ordinal)
            ? value.Length <= 2
            : value.Length <= 1 || !value.StartsWith("-", StringComparison.Ordinal)))
        {
            return false;
        }

        var start = value.StartsWith("--", StringComparison.Ordinal) ? 2 : 1;
        return value[start..].All(character => char.IsLetterOrDigit(character) || character is '-' or '_' or '.');
    }
}

public sealed record CustomTemplateHelpDiscoveryResult
{
    public CustomTemplateHelpDiscoveryResult(
        EnvironmentDetectionStatus status,
        CustomTemplateHelpParseMode mode,
        IReadOnlyList<CustomTemplateHelpOption> options,
        bool allowsRestrictedAdditionalArguments,
        IReadOnlyList<string> rawStandardOutput,
        IReadOnlyList<string> rawStandardError,
        IReadOnlyList<string> diagnostics)
    {
        Status = status;
        Mode = mode;
        Options = Array.AsReadOnly(options.ToArray());
        AllowsRestrictedAdditionalArguments = allowsRestrictedAdditionalArguments;
        RawStandardOutput = Array.AsReadOnly(rawStandardOutput.ToArray());
        RawStandardError = Array.AsReadOnly(rawStandardError.ToArray());
        Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
    }

    public EnvironmentDetectionStatus Status { get; }
    public CustomTemplateHelpParseMode Mode { get; }
    public IReadOnlyList<CustomTemplateHelpOption> Options { get; }
    public bool AllowsRestrictedAdditionalArguments { get; }
    public IReadOnlyList<string> RawStandardOutput { get; }
    public IReadOnlyList<string> RawStandardError { get; }
    public IReadOnlyList<string> Diagnostics { get; }
}
