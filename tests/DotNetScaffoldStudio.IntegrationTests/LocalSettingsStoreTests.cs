using DotNetScaffoldStudio.Application;
using DotNetScaffoldStudio.Domain;
using DotNetScaffoldStudio.Infrastructure;

namespace DotNetScaffoldStudio.IntegrationTests;

public sealed class LocalSettingsStoreTests
{
    [Fact]
    public async Task SaveAsync_WritesSchemaVersionAndReplacesWithoutLeavingTemporaryFiles()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var settingsPath = Path.Combine(root, "settings.json");
            var store = new LocalSettingsStore(settingsPath);

            await store.SaveAsync(LocalSettingsDocument.Default, CancellationToken.None);

            var json = await File.ReadAllTextAsync(settingsPath);
            Assert.Contains("\"schemaVersion\": 1", json, StringComparison.Ordinal);
            Assert.Contains("\"recentWorkspaces\": []", json, StringComparison.Ordinal);
            Assert.Contains("\"width\": 1360", json, StringComparison.Ordinal);
            Assert.Contains("\"isNavigationCollapsed\": false", json, StringComparison.Ordinal);
            var result = await store.LoadAsync(CancellationToken.None);
            Assert.Equal(LocalSettingsLoadStatus.Loaded, result.Status);
            Assert.Equal(LocalSettingsDocument.CurrentSchemaVersion, result.Settings.SchemaVersion);
            Assert.Single(Directory.GetFiles(root), settingsPath);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task SaveAsync_WhenWriteIsInterrupted_LeavesPreviousSettingsReadable()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var settingsPath = Path.Combine(root, "settings.json");
            var store = new LocalSettingsStore(settingsPath);
            await store.SaveAsync(LocalSettingsDocument.Default, CancellationToken.None);

            var interruptedStore = new LocalSettingsStore(
                settingsPath,
                new InterruptedAtomicFileReplacer());

            await Assert.ThrowsAsync<IOException>(() =>
                interruptedStore.SaveAsync(LocalSettingsDocument.Default, CancellationToken.None));

            var result = await store.LoadAsync(CancellationToken.None);
            Assert.Equal(LocalSettingsLoadStatus.Loaded, result.Status);
            Assert.Equal(LocalSettingsDocument.CurrentSchemaVersion, result.Settings.SchemaVersion);
            Assert.Contains("\"schemaVersion\": 1", await File.ReadAllTextAsync(settingsPath), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task LoadAsync_WhenJsonIsInvalid_QuarantinesFileAndReturnsDefaults()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var settingsPath = Path.Combine(root, "settings.json");
            await File.WriteAllTextAsync(settingsPath, "{ invalid json");

            var result = await new LocalSettingsStore(settingsPath).LoadAsync(CancellationToken.None);

            Assert.Equal(LocalSettingsLoadStatus.RecoveredFromCorruptFile, result.Status);
            Assert.True(result.UsedDefaults);
            Assert.Equal(LocalSettingsDocument.CurrentSchemaVersion, result.Settings.SchemaVersion);
            Assert.False(File.Exists(settingsPath));
            Assert.NotNull(result.QuarantinedPath);
            Assert.True(File.Exists(result.QuarantinedPath));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task LoadAsync_WhenSchemaVersionIsUnsupported_QuarantinesFileAndReturnsDefaults()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var settingsPath = Path.Combine(root, "settings.json");
            await File.WriteAllTextAsync(settingsPath, "{\"schemaVersion\":999}");

            var result = await new LocalSettingsStore(settingsPath).LoadAsync(CancellationToken.None);

            Assert.Equal(LocalSettingsLoadStatus.RecoveredFromUnsupportedSchema, result.Status);
            Assert.True(result.UsedDefaults);
            Assert.False(File.Exists(settingsPath));
            Assert.NotNull(result.QuarantinedPath);
            Assert.True(File.Exists(result.QuarantinedPath));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"dotnet-scaffold-studio-settings-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class InterruptedAtomicFileReplacer : IAtomicFileReplacer
    {
        public async Task ReplaceAsync(
            string destinationPath,
            ReadOnlyMemory<byte> content,
            CancellationToken cancellationToken)
        {
            var interruptedPath = $"{destinationPath}.interrupted.tmp";
            var partialLength = Math.Max(1, content.Length / 2);
            await File.WriteAllBytesAsync(interruptedPath, content[..partialLength].ToArray(), cancellationToken);
            throw new IOException();
        }
    }
}
