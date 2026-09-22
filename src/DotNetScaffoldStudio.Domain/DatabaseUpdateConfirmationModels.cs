namespace DotNetScaffoldStudio.Domain;

public enum DatabaseConnectionSource
{
    ProjectConfiguration,
    NamedConnection,
    DirectConnectionString
}

public sealed record DatabaseUpdateConfirmationData(
    string TargetProjectPath,
    string? DbContext,
    DatabaseConnectionSource ConnectionSource,
    string MaskedCommandPreview,
    string RiskReason);
