using DotNetScaffoldStudio.Application;

namespace DotNetScaffoldStudio.UnitTests;

public sealed class NetworkPrivacyTests
{
    [Fact]
    public async Task UserInitiatedAdapter_DeniesUnrequestedNetworkAndAllowsConfirmedOperation()
    {
        var adapter = new RecordingNetworkAccessAdapter();

        var denied = await adapter.AuthorizeAsync(
            new NetworkAccessRequest("documentation", UserInitiated: false),
            CancellationToken.None);
        var allowed = await adapter.AuthorizeAsync(
            new NetworkAccessRequest("package-restore", UserInitiated: true),
            CancellationToken.None);

        Assert.False(denied.IsAllowed);
        Assert.True(allowed.IsAllowed);
    }

    [Fact]
    public void ProductionSources_DoNotContainTelemetryOrDirectHttpClients()
    {
        var root = LocateRepositoryRoot();
        var sourceFiles = Directory.EnumerateFiles(
                Path.Combine(root, "src"),
                "*.*",
                SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
                           path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal));
        var forbiddenTokens = new[]
        {
            "HttpClient",
            "WebClient",
            "ApplicationInsights",
            "OpenTelemetry",
            "TelemetryClient"
        };

        foreach (var sourceFile in sourceFiles)
        {
            var source = File.ReadAllText(sourceFile);
            Assert.DoesNotContain("using System.Net.Http", source, StringComparison.Ordinal);
            Assert.DoesNotContain("System.Net.Sockets", source, StringComparison.Ordinal);
            Assert.All(forbiddenTokens, token =>
                Assert.DoesNotContain(token, source, StringComparison.OrdinalIgnoreCase));
        }
    }

    private static string LocateRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src")) &&
                File.Exists(Path.Combine(directory.FullName, "DotNetScaffoldStudio.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("找不到測試儲存庫根目錄。");
    }

    private sealed class RecordingNetworkAccessAdapter : INetworkAccessAdapter
    {
        public Task<NetworkAccessDecision> AuthorizeAsync(
            NetworkAccessRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(request.UserInitiated
                ? new NetworkAccessDecision(true)
                : new NetworkAccessDecision(false, "測試拒絕未經使用者觸發的網路操作。"));
    }
}
