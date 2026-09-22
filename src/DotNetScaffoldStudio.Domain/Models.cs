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
    string? AvailabilityReason = null)
{
    public bool IsAvailable => Availability == FeatureAvailability.Available;

    public string AvailabilityLabel => Availability switch
    {
        FeatureAvailability.Available => "可用",
        FeatureAvailability.MissingDependency => "缺少相依性",
        FeatureAvailability.UnsupportedPlatform => "平台不支援",
        _ => "狀態未知"
    };

    public string RiskLabel => Risk switch
    {
        FeatureRisk.Normal => "一般風險",
        FeatureRisk.FileOverwrite => "可能覆寫檔案",
        FeatureRisk.FileRemoval => "可能移除檔案",
        FeatureRisk.DatabaseChange => "資料庫變更",
        FeatureRisk.EnvironmentChange => "環境變更",
        _ => "風險未知"
    };

    public string DependencySummary => CommandKind switch
    {
        "dotnet-new" => ".NET SDK 與對應範本",
        "aspnet-codegenerator" => ".NET SDK、Scaffolding 工具與 CodeGeneration 套件",
        "dotnet-ef" => ".NET SDK、dotnet-ef 與 EF Core 套件",
        _ => "依目前工作區設定"
    };
}

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
