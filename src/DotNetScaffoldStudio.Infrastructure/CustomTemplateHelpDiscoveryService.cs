using System.ComponentModel;
using DotNetScaffoldStudio.Application;
using DotNetScaffoldStudio.Domain;

namespace DotNetScaffoldStudio.Infrastructure;

public sealed class CustomTemplateHelpDiscoveryService(
    ICommandRunner runner,
    string dotNetExecutable = "dotnet") : ICustomTemplateHelpDiscovery
{
    private static readonly IReadOnlyDictionary<string, string> CommandEnvironment = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1",
        ["DOTNET_CLI_UI_LANGUAGE"] = "en",
        ["DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE"] = "true",
        ["DOTNET_NOLOGO"] = "1",
        ["LC_ALL"] = "C"
    };

    public async Task<CustomTemplateHelpDiscoveryResult> DiscoverAsync(
        string workspaceRoot,
        string templateShortName,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ValidateTemplateShortName(templateShortName);
        var root = Path.GetFullPath(workspaceRoot);
        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException($"找不到工作區：{root}");
        }

        var request = new CommandRequest(
            dotNetExecutable,
            [
                new CommandArgument("new"),
                new CommandArgument(templateShortName),
                new CommandArgument("--help")
            ],
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
            return CreateFallbackResult(
                EnvironmentDetectionStatus.Missing,
                [],
                [],
                [$"dotnet new {templateShortName} --help 啟動失敗：{exception.Message}"]);
        }
        catch (Exception exception) when (exception is Win32Exception or IOException or InvalidOperationException)
        {
            return CreateFallbackResult(
                EnvironmentDetectionStatus.DetectionFailed,
                [],
                [],
                [$"dotnet new {templateShortName} --help 啟動失敗：{exception.Message}"]);
        }

        if (result.WasCancelled && cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }

        if (!result.Succeeded)
        {
            return new CustomTemplateHelpDiscoveryResult(
                EnvironmentDetectionStatus.DetectionFailed,
                CustomTemplateHelpParseMode.SafeFallback,
                [],
                true,
                result.StandardOutput,
                result.StandardError,
                [CreateFailureDiagnostic(templateShortName, result)]);
        }

        var parsed = CustomTemplateHelpParser.Parse(result.StandardOutput);
        return new CustomTemplateHelpDiscoveryResult(
            EnvironmentDetectionStatus.Available,
            parsed.Mode,
            parsed.Options,
            parsed.Mode != CustomTemplateHelpParseMode.Parsed,
            result.StandardOutput,
            result.StandardError,
            parsed.Diagnostics);
    }

    private static void ValidateTemplateShortName(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value.StartsWith("-", StringComparison.Ordinal) ||
            value.Any(character => char.IsWhiteSpace(character) || char.IsControl(character)))
        {
            throw new ArgumentException("範本短名稱必須是單一安全識別字。", nameof(value));
        }
    }

    private static CustomTemplateHelpDiscoveryResult CreateFallbackResult(
        EnvironmentDetectionStatus status,
        IReadOnlyList<string> standardOutput,
        IReadOnlyList<string> standardError,
        IReadOnlyList<string> diagnostics) =>
        new(
            status,
            CustomTemplateHelpParseMode.SafeFallback,
            [],
            true,
            standardOutput,
            standardError,
            diagnostics);

    private static string CreateFailureDiagnostic(string templateShortName, CommandResult result)
    {
        var error = string.Join(Environment.NewLine, result.StandardError);
        return string.IsNullOrWhiteSpace(error)
            ? $"dotnet new {templateShortName} --help 失敗，結束碼 {result.ExitCode}。"
            : $"dotnet new {templateShortName} --help 失敗，結束碼 {result.ExitCode}：{error}";
    }
}
