namespace DotNetScaffoldStudio.Domain;

public enum LocalSettingsLoadStatus
{
    Defaults,
    Loaded,
    RecoveredFromCorruptFile,
    RecoveredFromUnsupportedSchema
}

public sealed record LocalSettingsDocument
{
    public const int CurrentSchemaVersion = 1;

    public LocalSettingsDocument(int schemaVersion)
    {
        if (schemaVersion < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(schemaVersion));
        }

        SchemaVersion = schemaVersion;
    }

    public int SchemaVersion { get; }

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
