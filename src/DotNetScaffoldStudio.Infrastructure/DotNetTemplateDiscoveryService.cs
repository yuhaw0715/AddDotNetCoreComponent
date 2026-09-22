using System.ComponentModel;
using DotNetScaffoldStudio.Application;
using DotNetScaffoldStudio.Domain;

namespace DotNetScaffoldStudio.Infrastructure;

public sealed class DotNetTemplateDiscoveryService(
    ICommandRunner runner,
    string dotNetExecutable = "dotnet") : IDotNetTemplateDiscovery
{
    private static readonly IReadOnlyDictionary<string, string> CommandEnvironment = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1",
        ["DOTNET_CLI_UI_LANGUAGE"] = "en",
        ["DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE"] = "true",
        ["DOTNET_NOLOGO"] = "1",
        ["LC_ALL"] = "C"
    };

    public async Task<DotNetTemplateDiscoveryResult> DiscoverAsync(
        string workspaceRoot,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        var root = Path.GetFullPath(workspaceRoot);
        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException($"找不到工作區：{root}");
        }

        var request = new CommandRequest(
            dotNetExecutable,
            [new CommandArgument("new"), new CommandArgument("list")],
            root,
            FeatureRisk.Normal,
            modifiesWorkspace: false,
            environment: CommandEnvironment);

        CommandResult result;
        try
        {
            result = await runner.RunAsync(request, null, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Win32Exception exception) when (exception.NativeErrorCode is 2 or 3)
        {
            return new DotNetTemplateDiscoveryResult(
                EnvironmentDetectionStatus.Missing,
                [],
                [],
                [],
                [$"dotnet new list 啟動失敗：{exception.Message}"]);
        }
        catch (Exception exception) when (exception is Win32Exception or IOException or InvalidOperationException)
        {
            return new DotNetTemplateDiscoveryResult(
                EnvironmentDetectionStatus.DetectionFailed,
                [],
                [],
                [],
                [$"dotnet new list 啟動失敗：{exception.Message}"]);
        }

        if (result.WasCancelled && cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }

        if (!result.Succeeded)
        {
            return new DotNetTemplateDiscoveryResult(
                EnvironmentDetectionStatus.DetectionFailed,
                [],
                result.StandardOutput,
                result.StandardError,
                [CreateFailureDiagnostic(result)]);
        }

        var parsed = DotNetTemplateListParser.Parse(result.StandardOutput);
        if (!parsed.IsValid)
        {
            return new DotNetTemplateDiscoveryResult(
                EnvironmentDetectionStatus.DetectionFailed,
                [],
                result.StandardOutput,
                result.StandardError,
                parsed.Diagnostics.Append("dotnet new list 回傳成功，但輸出格式無法辨識。").ToArray());
        }

        var status = parsed.Templates.Count == 0
            ? EnvironmentDetectionStatus.Missing
            : EnvironmentDetectionStatus.Available;
        return new DotNetTemplateDiscoveryResult(
            status,
            parsed.Templates,
            result.StandardOutput,
            result.StandardError,
            parsed.Diagnostics);
    }

    private static string CreateFailureDiagnostic(CommandResult result)
    {
        var error = string.Join(Environment.NewLine, result.StandardError);
        return string.IsNullOrWhiteSpace(error)
            ? $"dotnet new list 失敗，結束碼 {result.ExitCode}。"
            : $"dotnet new list 失敗，結束碼 {result.ExitCode}：{error}";
    }
}
