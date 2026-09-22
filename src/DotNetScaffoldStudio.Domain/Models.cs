namespace DotNetScaffoldStudio.Domain;

public enum FeatureRisk
{
    Normal,
    FileOverwrite,
    FileRemoval,
    DatabaseChange,
    EnvironmentChange
}

public enum FeatureAvailability
{
    Available,
    MissingDependency,
    UnsupportedPlatform
}

public sealed record NavigationItem(string Id, string Title, string Subtitle)
{
    public string Glyph => Id switch
    {
        "home" => "⌂",
        "project" => "▦",
        "component" => "◇",
        "scaffolding" => "⚙",
        "efcore" => "◈",
        "custom" => "✦",
        "history" => "↺",
        "settings" => "⚑",
        _ => "•"
    };
}

public sealed record FeatureDefinition(
    string Id,
    string Category,
    string DisplayName,
    string ShortName,
    string Description,
    string Badge,
    string CommandKind,
    FeatureRisk Risk = FeatureRisk.Normal,
    FeatureAvailability Availability = FeatureAvailability.Available,
    string? AvailabilityReason = null);

public sealed record ProjectInfo(string Name, string Path, string TargetFramework, string Sdk, string RelativePath = "")
{
    public string DisplayText => $"{Name}  ·  {TargetFramework}";
}

public enum WorkspaceSelectionKind
{
    Folder,
    Solution,
    SolutionXml
}

public sealed record WorkspaceSelection(string SelectedPath, string RootPath, WorkspaceSelectionKind Kind);

public sealed record TargetValidationResult(bool IsValid, string? ErrorMessage)
{
    public static TargetValidationResult Valid { get; } = new(true, null);
}

public sealed record CommandPreview(
    string Executable,
    IReadOnlyList<string> Arguments,
    string WorkingDirectory,
    FeatureRisk Risk)
{
    public string DisplayText => string.Join(' ', new[] { Executable }.Concat(Arguments.Select(Quote)));

    private static string Quote(string value)
    {
        if (value.Length > 0 && value.All(character => char.IsLetterOrDigit(character) || "-._/:=".Contains(character)))
        {
            return value;
        }

        return $"\"{value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal)}\"";
    }
}

public sealed record DemoExecutionResult(
    bool Succeeded,
    string Title,
    string Summary,
    IReadOnlyList<string> Output,
    IReadOnlyList<string> FileChanges);
