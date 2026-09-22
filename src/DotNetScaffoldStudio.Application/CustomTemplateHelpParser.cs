using System.Text.RegularExpressions;
using DotNetScaffoldStudio.Domain;

namespace DotNetScaffoldStudio.Application;

public sealed record CustomTemplateHelpParseResult(
    CustomTemplateHelpParseMode Mode,
    IReadOnlyList<CustomTemplateHelpOption> Options,
    IReadOnlyList<string> Diagnostics);

public static class CustomTemplateHelpParser
{
    private static readonly Regex OptionDeclarationPattern = new(
        @"^\s*(?<spec>-{1,2}[A-Za-z][A-Za-z0-9._-]*(?:\s*,\s*-{1,2}[A-Za-z][A-Za-z0-9._-]*)*)(?:(?:\s+|=)(?<placeholder><[^>\r\n]+>))?\s{2,}(?<description>\S.*)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex ChoiceValuePattern = new(
        @"^(?<value>[^\s]+)\s{2,}.*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static CustomTemplateHelpParseResult Parse(IEnumerable<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var output = lines.ToArray();
        var sectionIndex = FindSectionIndex(output, "Template options:");
        var usedGeneralOptions = false;
        if (sectionIndex < 0)
        {
            sectionIndex = FindSectionIndex(output, "Options:");
            usedGeneralOptions = sectionIndex >= 0;
        }

        if (sectionIndex < 0)
        {
            return Fallback("找不到範本說明中的 Options 或 Template options 區段。");
        }

        var builders = new List<OptionBuilder>();
        var diagnostics = new List<string>();
        var partiallyParsed = usedGeneralOptions;
        OptionBuilder? current = null;

        foreach (var line in output.Skip(sectionIndex + 1))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("To see help", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("For more information", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            if (line == line.TrimStart() && trimmed.EndsWith(':') &&
                !trimmed.StartsWith("Type:", StringComparison.OrdinalIgnoreCase) &&
                !trimmed.StartsWith("Default:", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            if (TryParseOptionDeclaration(line, out var declaration))
            {
                Flush(current, builders, diagnostics, ref partiallyParsed);
                current = new OptionBuilder(declaration);
                continue;
            }

            if (current is null || trimmed.Length == 0)
            {
                continue;
            }

            if (current.TryParseMetadata(trimmed, diagnostics, ref partiallyParsed))
            {
                continue;
            }

            if (current.Kind == ParameterValueKind.Enumeration && TryParseChoiceValue(trimmed, out var choice))
            {
                current.AllowedValues.Add(choice);
                continue;
            }

            if (line.TrimStart().StartsWith("-", StringComparison.Ordinal))
            {
                partiallyParsed = true;
                diagnostics.Add($"略過無法辨識的範本選項列：{trimmed}");
                continue;
            }

            current.DescriptionLines.Add(trimmed);
        }

        Flush(current, builders, diagnostics, ref partiallyParsed);
        if (builders.Count == 0)
        {
            return Fallback(
                diagnostics.Append("沒有可靠辨識的範本選項，將使用受限制的額外參數模式。").ToArray());
        }

        var mode = partiallyParsed
            ? CustomTemplateHelpParseMode.PartiallyParsed
            : CustomTemplateHelpParseMode.Parsed;
        return new CustomTemplateHelpParseResult(mode, builders.Select(builder => builder.Build()).ToArray(), diagnostics);
    }

    private static int FindSectionIndex(IReadOnlyList<string> lines, string sectionName) =>
        Enumerable.Range(0, lines.Count)
            .FirstOrDefault(index => lines[index].Trim().Equals(sectionName, StringComparison.OrdinalIgnoreCase), -1);

    private static bool TryParseOptionDeclaration(string line, out OptionDeclaration declaration)
    {
        var match = OptionDeclarationPattern.Match(line);
        if (!match.Success)
        {
            declaration = default;
            return false;
        }

        var names = match.Groups["spec"].Value
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var name = names.FirstOrDefault(value => value.StartsWith("--", StringComparison.Ordinal)) ?? names[0];
        var alias = names.FirstOrDefault(value => !value.Equals(name, StringComparison.Ordinal));
        declaration = new OptionDeclaration(
            name,
            alias,
            match.Groups["placeholder"].Success ? match.Groups["placeholder"].Value : null,
            match.Groups["description"].Value.Trim());
        return true;
    }

    private static bool TryParseChoiceValue(string line, out string value)
    {
        var match = ChoiceValuePattern.Match(line);
        value = match.Success ? match.Groups["value"].Value.Trim() : string.Empty;
        return match.Success && value.Length > 0;
    }

    private static void Flush(
        OptionBuilder? builder,
        ICollection<OptionBuilder> builders,
        ICollection<string> diagnostics,
        ref bool partiallyParsed)
    {
        if (builder is null)
        {
            return;
        }

        if (builder.DescriptionLines.Count == 0)
        {
            partiallyParsed = true;
            diagnostics.Add($"範本選項 {builder.Name} 缺少說明，已略過。");
            return;
        }

        if (builder.Kind == ParameterValueKind.Enumeration && builder.AllowedValues.Count == 0)
        {
            partiallyParsed = true;
            diagnostics.Add($"範本選項 {builder.Name} 宣告為 choice，但沒有可辨識的值。");
            builder.DeclaredKind = ParameterValueKind.Text;
        }

        builders.Add(builder);
    }

    private static CustomTemplateHelpParseResult Fallback(string diagnostic) =>
        Fallback([diagnostic]);

    private static CustomTemplateHelpParseResult Fallback(IReadOnlyList<string> diagnostics) =>
        new(CustomTemplateHelpParseMode.SafeFallback, [], diagnostics);

    private readonly record struct OptionDeclaration(
        string Name,
        string? Alias,
        string? Placeholder,
        string Description);

    private sealed class OptionBuilder(OptionDeclaration declaration)
    {
        public string Name { get; } = declaration.Name;
        public string? Alias { get; } = declaration.Alias;
        public string? Placeholder { get; } = declaration.Placeholder;
        public List<string> DescriptionLines { get; } = [declaration.Description];
        public List<string> AllowedValues { get; } = [];
        public ParameterValueKind? DeclaredKind { get; set; }
        public ParameterValueKind Kind => DeclaredKind ?? InferKind();
        public string? DefaultValue { get; private set; }

        public bool TryParseMetadata(
            string line,
            ICollection<string> diagnostics,
            ref bool partiallyParsed)
        {
            if (line.StartsWith("Type:", StringComparison.OrdinalIgnoreCase))
            {
                var type = line["Type:".Length..].Trim();
                DeclaredKind = type.ToLowerInvariant() switch
                {
                    "bool" or "boolean" => ParameterValueKind.Boolean,
                    "choice" or "enum" or "enumeration" => ParameterValueKind.Enumeration,
                    "path" => ParameterValueKind.Path,
                    "text" or "string" => ParameterValueKind.Text,
                    _ => null
                };
                if (DeclaredKind is null)
                {
                    partiallyParsed = true;
                    diagnostics.Add($"範本選項 {Name} 使用未知型別：{type}。");
                }

                return true;
            }

            if (line.StartsWith("Default:", StringComparison.OrdinalIgnoreCase))
            {
                DefaultValue = line["Default:".Length..].Trim();
                return true;
            }

            return false;
        }

        public CustomTemplateHelpOption Build()
        {
            var description = string.Join(' ', DescriptionLines).Trim();
            var isRequired = Placeholder?.Contains("required", StringComparison.OrdinalIgnoreCase) == true ||
                description.Contains("required", StringComparison.OrdinalIgnoreCase);
            return new CustomTemplateHelpOption(
                Name,
                Alias,
                Kind,
                Placeholder,
                description,
                isRequired,
                AllowedValues,
                DefaultValue);
        }

        private ParameterValueKind InferKind()
        {
            if (AllowedValues.Count > 0)
            {
                return ParameterValueKind.Enumeration;
            }

            if (Placeholder is null)
            {
                return ParameterValueKind.Boolean;
            }

            return Placeholder.Contains("path", StringComparison.OrdinalIgnoreCase) ||
                Placeholder.Contains("directory", StringComparison.OrdinalIgnoreCase) ||
                Placeholder.Contains("file", StringComparison.OrdinalIgnoreCase)
                ? ParameterValueKind.Path
                : ParameterValueKind.Text;
        }
    }
}
