using System.Text.RegularExpressions;
using DotNetScaffoldStudio.Domain;

namespace DotNetScaffoldStudio.Application;

public sealed record DotNetTemplateListParseResult(
    bool IsValid,
    IReadOnlyList<LocalTemplateCapability> Templates,
    IReadOnlyList<string> Diagnostics);

public static partial class DotNetTemplateListParser
{
    private static readonly Regex ColumnSeparatorPattern = new(@"^-+(?:\s+-+){3,}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex DashGroupPattern = new(@"-{2,}", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex ShortNameSeparatorPattern = new(@"\s*(?:,|;|\|)\s*|\s{2,}", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex LanguagePattern = new(@"\[(?<value>[^\]]+)\]|(?<value>[A-Za-z][A-Za-z0-9#+.\-]*)", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static DotNetTemplateListParseResult Parse(IEnumerable<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var output = lines.ToArray();
        var headerIndex = FindHeaderIndex(output);
        if (headerIndex < 0)
        {
            var hasNoTemplatesMessage = output.Any(line =>
                line.Contains("No templates found", StringComparison.OrdinalIgnoreCase));
            return hasNoTemplatesMessage
                ? new DotNetTemplateListParseResult(true, [], [])
                : new DotNetTemplateListParseResult(false, [], ["找不到 dotnet new list 的欄位標題。"]);
        }

        var header = output[headerIndex];
        var separatorIndex = FindSeparatorIndex(output, headerIndex);
        var starts = separatorIndex >= 0
            ? GetColumnStarts(output[separatorIndex])
            : GetColumnStartsFromHeader(header);

        if (starts.Count < 4)
        {
            return new DotNetTemplateListParseResult(false, [], ["找不到 dotnet new list 的完整欄位分隔。"]);
        }

        var templates = new List<LocalTemplateCapability>();
        var diagnostics = new List<string>();
        var dataStart = separatorIndex >= 0 ? separatorIndex + 1 : headerIndex + 1;
        foreach (var line in output.Skip(dataStart))
        {
            if (string.IsNullOrWhiteSpace(line) || IsSeparator(line) || IsInformationalLine(line))
            {
                continue;
            }

            var displayName = Slice(line, starts, 0);
            var shortNames = ParseShortNames(Slice(line, starts, 1));
            if (displayName.Length == 0 && shortNames.Count == 0)
            {
                continue;
            }

            if (displayName.Length == 0 || shortNames.Count == 0)
            {
                diagnostics.Add($"略過無法辨識的範本列：{line.Trim()}");
                continue;
            }

            templates.Add(new LocalTemplateCapability(
                displayName,
                shortNames,
                ParseLanguages(Slice(line, starts, 2)),
                ParseTags(Slice(line, starts, 3))));
        }

        return new DotNetTemplateListParseResult(true, templates.AsReadOnly(), diagnostics.AsReadOnly());
    }

    private static int FindHeaderIndex(IReadOnlyList<string> lines) =>
        Enumerable.Range(0, lines.Count)
            .FirstOrDefault(index =>
                ContainsHeader(lines[index], "Template Name") &&
                ContainsHeader(lines[index], "Short Name") &&
                ContainsHeader(lines[index], "Language") &&
                ContainsHeader(lines[index], "Tags"), -1);

    private static int FindSeparatorIndex(IReadOnlyList<string> lines, int headerIndex) =>
        Enumerable.Range(headerIndex + 1, lines.Count - headerIndex - 1)
            .FirstOrDefault(index => IsSeparator(lines[index]), -1);

    private static List<int> GetColumnStarts(string line) =>
        DashGroupPattern.Matches(line).Select(match => match.Index).Take(4).ToList();

    private static List<int> GetColumnStartsFromHeader(string header)
    {
        var labels = new[] { "Template Name", "Short Name", "Language", "Tags" };
        return labels
            .Select(label => header.IndexOf(label, StringComparison.OrdinalIgnoreCase))
            .Where(index => index >= 0)
            .ToList();
    }

    private static bool ContainsHeader(string line, string label) =>
        line.Contains(label, StringComparison.OrdinalIgnoreCase);

    private static bool IsSeparator(string line) =>
        ColumnSeparatorPattern.IsMatch(line.Trim());

    private static bool IsInformationalLine(string line)
    {
        var trimmed = line.Trim();
        return trimmed.StartsWith("These templates ", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("No templates found", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("For more information", StringComparison.OrdinalIgnoreCase);
    }

    private static string Slice(string line, IReadOnlyList<int> starts, int column)
    {
        var start = starts[column];
        if (start >= line.Length)
        {
            return string.Empty;
        }

        var end = column + 1 < starts.Count ? Math.Min(starts[column + 1], line.Length) : line.Length;
        return line[start..end].Trim();
    }

    private static IReadOnlyList<string> ParseShortNames(string value) =>
        ShortNameSeparatorPattern.Split(value.Trim())
            .Select(item => item.Trim())
            .Where(item => item.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    private static IReadOnlyList<string> ParseLanguages(string value) =>
        LanguagePattern.Matches(value)
            .Select(match => match.Groups["value"].Value.Trim())
            .Where(item => item.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static IReadOnlyList<string> ParseTags(string value) =>
        value.Trim()
            .Split(['/', ';', ','], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
}
