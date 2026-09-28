using System.Text.Json;
using DotNetScaffoldStudio.Application;
using DotNetScaffoldStudio.Domain;

namespace DotNetScaffoldStudio.UnitTests;

public sealed class ExecutionHistoryTests
{
    [Fact]
    public void Record_SerializesOnlyMaskedCommandAndOutput()
    {
        const string secret = "Server=db;Password=secret;Token=abc";
        var requestWithSecret = new CommandRequest(
            "dotnet",
            [new CommandArgument("ef"), new CommandArgument(secret, isSecret: true)],
            "/tmp/workspace",
            FeatureRisk.DatabaseChange,
            modifiesWorkspace: true);
        var startedAt = DateTimeOffset.UtcNow;
        var command = new CommandResult(
            1,
            startedAt,
            startedAt.AddSeconds(1),
            [$"stdout: {secret}"],
            [$"stderr: {secret}"]);
        var history = new SessionExecutionHistory();

        history.Record(
            requestWithSecret,
            new CommandExecutionResult(command, null),
            feature: "database-update",
            resultSummary: $"失敗：{secret}");

        var entry = Assert.Single(history.Entries);
        var serialized = history.Serialize();

        Assert.Equal("database-update", entry.Feature);
        Assert.Contains(CommandPreviewFormatter.SecretMask, entry.MaskedCommand, StringComparison.Ordinal);
        Assert.All(entry.Output, line =>
        {
            Assert.Contains(CommandPreviewFormatter.SecretMask, line.Text, StringComparison.Ordinal);
            Assert.DoesNotContain(secret, line.Text, StringComparison.Ordinal);
        });
        Assert.DoesNotContain(secret, entry.ResultSummary, StringComparison.Ordinal);
        Assert.DoesNotContain(secret, serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("CommandRequest", serialized, StringComparison.Ordinal);
        Assert.NotEmpty(JsonDocument.Parse(serialized).RootElement.EnumerateArray());
    }

    [Fact]
    public void RecordPreview_PreservesOnlyCurrentSessionEntries()
    {
        var history = new SessionExecutionHistory();
        var startedAt = DateTimeOffset.UtcNow;

        history.RecordPreview(
            "controller",
            "dotnet aspnet-codegenerator controller --controllerName OrdersController",
            "/tmp/workspace",
            startedAt,
            startedAt.AddMilliseconds(10),
            0,
            wasCancelled: false,
            "完成");

        var entry = Assert.Single(history.Entries);
        Assert.Equal(0, entry.ExitCode);
        Assert.False(entry.WasCancelled);
        Assert.DoesNotContain("Password", history.Serialize(), StringComparison.OrdinalIgnoreCase);
    }
}
