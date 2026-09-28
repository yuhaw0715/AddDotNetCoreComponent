using DotNetScaffoldStudio.Domain;

namespace DotNetScaffoldStudio.Application;

public interface ILocalSettingsStore
{
    string SettingsPath { get; }

    Task<LocalSettingsLoadResult> LoadAsync(CancellationToken cancellationToken);

    Task SaveAsync(LocalSettingsDocument settings, CancellationToken cancellationToken);
}

public interface IAtomicFileReplacer
{
    Task ReplaceAsync(
        string destinationPath,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken);
}
