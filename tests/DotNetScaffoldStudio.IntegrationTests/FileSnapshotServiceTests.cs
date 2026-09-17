using DotNetScaffoldStudio.Domain;
using DotNetScaffoldStudio.Infrastructure;

namespace DotNetScaffoldStudio.IntegrationTests;

public sealed class FileSnapshotServiceTests
{
    [Fact]
    public async Task Compare_ReportsAddedModifiedDeletedAndExcludesBuildOutputs()
    {
        var root = Path.Combine(Path.GetTempPath(), $"dotnet-scaffold-studio-snapshot-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var service = new FileSnapshotService();
            await File.WriteAllTextAsync(Path.Combine(root, "modified.txt"), "before");
            await File.WriteAllTextAsync(Path.Combine(root, "deleted.txt"), "delete me");
            Directory.CreateDirectory(Path.Combine(root, "bin"));
            await File.WriteAllTextAsync(Path.Combine(root, "bin", "ignored.dll"), "before");
            var before = await service.CaptureAsync(root, CancellationToken.None);

            await File.WriteAllTextAsync(Path.Combine(root, "modified.txt"), "after with a different length");
            File.Delete(Path.Combine(root, "deleted.txt"));
            await File.WriteAllTextAsync(Path.Combine(root, "added.txt"), "new");
            await File.WriteAllTextAsync(Path.Combine(root, "bin", "ignored.dll"), "after");
            var after = await service.CaptureAsync(root, CancellationToken.None);

            var changes = service.Compare(before, after);

            Assert.Contains(new FileChange(FileChangeKind.Added, "added.txt"), changes);
            Assert.Contains(new FileChange(FileChangeKind.Modified, "modified.txt"), changes);
            Assert.Contains(new FileChange(FileChangeKind.Deleted, "deleted.txt"), changes);
            Assert.DoesNotContain(changes, change => change.RelativePath.Contains("ignored", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Compare_AfterCancelledCommandStillReportsPartialOutput()
    {
        var root = Path.Combine(Path.GetTempPath(), $"dotnet-scaffold-studio-cancelled-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var service = new FileSnapshotService();
            var before = await service.CaptureAsync(root, CancellationToken.None);
            await File.WriteAllTextAsync(Path.Combine(root, "PartiallyGenerated.cs"), "partial output");

            var afterCancellation = await service.CaptureAsync(root, CancellationToken.None);
            var changes = service.Compare(before, afterCancellation);

            Assert.Contains(new FileChange(FileChangeKind.Added, "PartiallyGenerated.cs"), changes);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
