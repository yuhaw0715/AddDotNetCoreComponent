using System.Text.Json.Serialization;

namespace DotNetScaffoldStudio.Domain;

public enum LocalSettingsLoadStatus
{
    Defaults,
    Loaded,
    RecoveredFromCorruptFile,
    RecoveredFromUnsupportedSchema
}

public sealed record RecentWorkspaceEntry
{
    [JsonConstructor]
    public RecentWorkspaceEntry(string? workspacePath, string? lastProjectPath)
    {
        WorkspacePath = workspacePath ?? string.Empty;
        LastProjectPath = lastProjectPath;
    }

    public string WorkspacePath { get; }
    public string? LastProjectPath { get; }
}

public sealed record LocalWindowSettings
{
    [JsonConstructor]
    public LocalWindowSettings(
        double width,
        double height,
        double? positionX,
        double? positionY,
        bool isMaximized)
    {
        Width = width;
        Height = height;
        PositionX = positionX;
        PositionY = positionY;
        IsMaximized = isMaximized;
    }

    public double Width { get; }
    public double Height { get; }
    public double? PositionX { get; }
    public double? PositionY { get; }
    public bool IsMaximized { get; }

    public static LocalWindowSettings Default { get; } = new(1360, 860, null, null, false);
}

public sealed record LocalSettingsPreferences
{
    [JsonConstructor]
    public LocalSettingsPreferences(string? selectedNavigationId, bool isNavigationCollapsed)
    {
        SelectedNavigationId = selectedNavigationId;
        IsNavigationCollapsed = isNavigationCollapsed;
    }

    public string? SelectedNavigationId { get; }
    public bool IsNavigationCollapsed { get; }

    public static LocalSettingsPreferences Default { get; } = new(null, false);
}

public sealed record LocalSettingsDocument
{
    public const int CurrentSchemaVersion = 1;

    [JsonConstructor]
    public LocalSettingsDocument(
        int schemaVersion,
        IReadOnlyList<RecentWorkspaceEntry>? recentWorkspaces = null,
        LocalWindowSettings? window = null,
        LocalSettingsPreferences? preferences = null)
    {
        if (schemaVersion < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(schemaVersion));
        }

        SchemaVersion = schemaVersion;
        RecentWorkspaces = Array.AsReadOnly((recentWorkspaces ?? []).ToArray());
        Window = window ?? LocalWindowSettings.Default;
        Preferences = preferences ?? LocalSettingsPreferences.Default;
    }

    public int SchemaVersion { get; }
    public IReadOnlyList<RecentWorkspaceEntry> RecentWorkspaces { get; }
    public LocalWindowSettings Window { get; }
    public LocalSettingsPreferences Preferences { get; }

    public static LocalSettingsDocument Default { get; } = new(CurrentSchemaVersion);
}

public sealed record LocalSettingsLoadResult(
    LocalSettingsDocument Settings,
    LocalSettingsLoadStatus Status,
    string? QuarantinedPath = null)
{
    public bool UsedDefaults => Status is
        LocalSettingsLoadStatus.Defaults or
        LocalSettingsLoadStatus.RecoveredFromCorruptFile or
        LocalSettingsLoadStatus.RecoveredFromUnsupportedSchema;
}
