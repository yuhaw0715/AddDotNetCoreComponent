using System.Text.Json;
using DotNetScaffoldStudio.Application;
using DotNetScaffoldStudio.Domain;

namespace DotNetScaffoldStudio.Infrastructure;

public sealed class LocalSettingsStore : ILocalSettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly IAtomicFileReplacer _fileReplacer;

    public LocalSettingsStore(
        string? settingsPath = null,
        IAtomicFileReplacer? fileReplacer = null)
    {
        SettingsPath = Path.GetFullPath(settingsPath ?? GetDefaultSettingsPath());
        _fileReplacer = fileReplacer ?? new AtomicFileReplacer();
    }

    public string SettingsPath { get; }

    public async Task<LocalSettingsLoadResult> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(SettingsPath))
        {
            return new(LocalSettingsDocument.Default, LocalSettingsLoadStatus.Defaults);
        }

        try
        {
            await using var stream = new FileStream(
                SettingsPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                bufferSize: 4096,
                useAsync: true);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var schemaVersion = ReadSchemaVersion(document.RootElement);

            if (schemaVersion != LocalSettingsDocument.CurrentSchemaVersion)
            {
                return new(
                    LocalSettingsDocument.Default,
                    LocalSettingsLoadStatus.RecoveredFromUnsupportedSchema,
                    QuarantineSettingsFile());
            }

            return new(
                new LocalSettingsDocument(schemaVersion),
                LocalSettingsLoadStatus.Loaded);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (JsonException)
        {
            return new(
                LocalSettingsDocument.Default,
                LocalSettingsLoadStatus.RecoveredFromCorruptFile,
                QuarantineSettingsFile());
        }
        catch (InvalidDataException)
        {
            return new(
                LocalSettingsDocument.Default,
                LocalSettingsLoadStatus.RecoveredFromCorruptFile,
                QuarantineSettingsFile());
        }
        catch (IOException)
        {
            return new(
                LocalSettingsDocument.Default,
                LocalSettingsLoadStatus.RecoveredFromCorruptFile,
                QuarantineSettingsFile());
        }
        catch (UnauthorizedAccessException)
        {
            return new(
                LocalSettingsDocument.Default,
                LocalSettingsLoadStatus.RecoveredFromCorruptFile,
                QuarantineSettingsFile());
        }
    }

    public async Task SaveAsync(
        LocalSettingsDocument settings,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (settings.SchemaVersion != LocalSettingsDocument.CurrentSchemaVersion)
        {
            throw new ArgumentException(nameof(settings));
        }

        var content = JsonSerializer.SerializeToUtf8Bytes(settings, SerializerOptions);
        await _fileReplacer.ReplaceAsync(SettingsPath, content, cancellationToken);
    }

    private static int ReadSchemaVersion(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("schemaVersion", out var schemaVersion) ||
            schemaVersion.ValueKind != JsonValueKind.Number ||
            !schemaVersion.TryGetInt32(out var version) ||
            version < 1)
        {
            throw new JsonException();
        }

        return version;
    }

    private string? QuarantineSettingsFile()
    {
        if (!File.Exists(SettingsPath))
        {
            return null;
        }

        var quarantinePath = $"{SettingsPath}.corrupt-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}";
        try
        {
            File.Move(SettingsPath, quarantinePath);
            return quarantinePath;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string GetDefaultSettingsPath()
    {
        var applicationData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(applicationData, "DotNetScaffoldStudio", "settings.json");
    }
}

public sealed class AtomicFileReplacer : IAtomicFileReplacer
{
    public async Task ReplaceAsync(
        string destinationPath,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);

        var fullDestinationPath = Path.GetFullPath(destinationPath);
        var directory = Path.GetDirectoryName(fullDestinationPath)
            ?? throw new ArgumentException(nameof(destinationPath));
        Directory.CreateDirectory(directory);

        var temporaryPath = Path.Combine(
            directory,
            $".{Path.GetFileName(fullDestinationPath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            await using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                options: FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await stream.WriteAsync(content, cancellationToken);
                await stream.FlushAsync(cancellationToken);
                stream.Flush(flushToDisk: true);
            }

            if (File.Exists(fullDestinationPath))
            {
                try
                {
                    File.Replace(temporaryPath, fullDestinationPath, destinationBackupFileName: null);
                }
                catch (PlatformNotSupportedException)
                {
                    File.Move(temporaryPath, fullDestinationPath, overwrite: true);
                }
            }
            else
            {
                File.Move(temporaryPath, fullDestinationPath);
            }
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                try
                {
                    File.Delete(temporaryPath);
                }
                catch (IOException)
                {
                    // Best effort cleanup; the destination was never exposed to the partial file.
                }
                catch (UnauthorizedAccessException)
                {
                    // Best effort cleanup; the destination was never exposed to the partial file.
                }
            }
        }
    }
}
