using System.ComponentModel;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DotNetScaffoldStudio.Application;
using DotNetScaffoldStudio.Domain;

namespace DotNetScaffoldStudio.Infrastructure;

public sealed class DotNetEnvironmentDiscoveryService(
    ICommandRunner runner,
    string dotNetExecutable = "dotnet") : IDotNetEnvironmentDiscovery
{
    private static readonly IReadOnlyDictionary<string, string> CommandEnvironment = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1",
        ["DOTNET_CLI_UI_LANGUAGE"] = "en",
        ["DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE"] = "true",
        ["DOTNET_NOLOGO"] = "1",
        ["LC_ALL"] = "C"
    };

    public async Task<DotNetEnvironmentSnapshot> DiscoverAsync(string workspaceRoot, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        var root = Path.GetFullPath(workspaceRoot);
        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException($"找不到工作區：{root}");
        }

        var diagnostics = new List<string>();
        var toolManifestExists = File.Exists(Path.Combine(root, ".config", "dotnet-tools.json"));
        var infoAttempt = await RunAsync(root, ["--info"], cancellationToken);
        if (infoAttempt.Result is not { Succeeded: true } infoResult)
        {
            var status = infoAttempt.IsMissingExecutable ? EnvironmentDetectionStatus.Missing : EnvironmentDetectionStatus.DetectionFailed;
            diagnostics.Add(infoAttempt.ToDiagnostic("dotnet --info"));
            return CreateFailedSnapshot(status, toolManifestExists, diagnostics);
        }

        var hostVersion = ParseInfoValue(infoResult.StandardOutput, "Host:", "Version:");
        var hostArchitecture = ParseInfoValue(infoResult.StandardOutput, "Host:", "Architecture:");
        var activeSdkVersion = ParseActiveSdkVersion(infoResult.StandardOutput);
        var sdkAttempt = await RunAsync(root, ["--list-sdks"], cancellationToken);
        var runtimeAttempt = await RunAsync(root, ["--list-runtimes"], cancellationToken);
        var sdkParse = sdkAttempt.Result is { Succeeded: true } sdkResult
            ? ParseSdks(sdkResult.StandardOutput)
            : ParsedList<DotNetSdkInstallation>.Failed;
        var sdks = sdkParse.Items;
        var hasSdk = sdkParse.IsValid && sdks.Count > 0;
        var workloadAttempt = hasSdk
            ? await RunAsync(root, ["workload", "list", "--machine-readable"], cancellationToken)
            : CommandAttempt.Skipped;
        var toolAttempt = toolManifestExists && hasSdk
            ? await RunAsync(root, ["tool", "list", "--local"], cancellationToken)
            : CommandAttempt.Skipped;

        var runtimeParse = runtimeAttempt.Result is { Succeeded: true } runtimeResult
            ? ParseRuntimes(runtimeResult.StandardOutput)
            : ParsedList<DotNetRuntimeInstallation>.Failed;
        var runtimes = runtimeParse.Items;
        var workloadParse = workloadAttempt.Result is { Succeeded: true } workloadResult
            ? ParseWorkloads(workloadResult.StandardOutput)
            : WorkloadParseResult.Failed;
        var toolParse = toolAttempt.Result is { Succeeded: true } toolResult
            ? ParseTools(toolResult.StandardOutput)
            : ParsedList<LocalDotNetTool>.Failed;
        var tools = toolParse.Items;

        AddFailureDiagnostic(diagnostics, sdkAttempt, "dotnet --list-sdks");
        AddFailureDiagnostic(diagnostics, runtimeAttempt, "dotnet --list-runtimes");
        AddFailureDiagnostic(diagnostics, workloadAttempt, "dotnet workload list");
        AddFailureDiagnostic(diagnostics, toolAttempt, "dotnet tool list --local");
        AddParseFailureDiagnostic(diagnostics, sdkAttempt, sdkParse.IsValid, "dotnet --list-sdks");
        AddParseFailureDiagnostic(diagnostics, runtimeAttempt, runtimeParse.IsValid, "dotnet --list-runtimes");
        AddParseFailureDiagnostic(diagnostics, toolAttempt, toolParse.IsValid, "dotnet tool list --local");
        if (workloadAttempt.Result is { Succeeded: true } && !workloadParse.IsValid)
        {
            diagnostics.Add("dotnet workload list 無法解析機器可讀輸出。");
        }

        if (!hasSdk && sdkAttempt.Result is { Succeeded: true })
        {
            diagnostics.Add("找不到已安裝的 .NET SDK，略過工作負載與本機工具清單偵測。");
        }

        var toolManifestStatus = toolManifestExists
            ? EnvironmentDetectionStatus.Available
            : EnvironmentDetectionStatus.Missing;
        var localToolStatus = !toolManifestExists
            ? EnvironmentDetectionStatus.Missing
            : toolAttempt.Result is not { Succeeded: true } || !toolParse.IsValid
                ? EnvironmentDetectionStatus.DetectionFailed
                : tools.Count > 0 ? EnvironmentDetectionStatus.Available : EnvironmentDetectionStatus.Missing;

        return new DotNetEnvironmentSnapshot(
            EnvironmentDetectionStatus.Available,
            hostVersion,
            hostArchitecture,
            activeSdkVersion,
            GetListStatus(sdkAttempt, sdkParse.IsValid, sdks.Count),
            sdks,
            GetListStatus(runtimeAttempt, runtimeParse.IsValid, runtimes.Count),
            runtimes,
            GetListStatus(workloadAttempt, workloadParse.IsValid, workloadParse.Workloads.Count),
            workloadParse.Workloads,
            toolManifestStatus,
            localToolStatus,
            tools,
            diagnostics.AsReadOnly());
    }

    private async Task<CommandAttempt> RunAsync(
        string workspaceRoot,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        var request = new CommandRequest(
            dotNetExecutable,
            arguments.Select(argument => new CommandArgument(argument)).ToArray(),
            workspaceRoot,
            FeatureRisk.Normal,
            modifiesWorkspace: false,
            environment: CommandEnvironment);

        try
        {
            var result = await runner.RunAsync(request, null, cancellationToken);
            if (result.WasCancelled && cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            return new CommandAttempt(result, null, false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Win32Exception exception) when (IsMissingExecutable(exception))
        {
            return new CommandAttempt(null, exception, true);
        }
        catch (Exception exception) when (exception is Win32Exception or IOException or InvalidOperationException)
        {
            return new CommandAttempt(null, exception, false);
        }
    }

    private static bool IsMissingExecutable(Win32Exception exception) => exception.NativeErrorCode is 2 or 3;

    private static EnvironmentDetectionStatus GetListStatus(CommandAttempt attempt, bool parseSucceeded, int itemCount) =>
        attempt.Result is not { Succeeded: true } || !parseSucceeded
            ? EnvironmentDetectionStatus.DetectionFailed
            : itemCount == 0 ? EnvironmentDetectionStatus.Missing : EnvironmentDetectionStatus.Available;

    private static void AddFailureDiagnostic(ICollection<string> diagnostics, CommandAttempt attempt, string command)
    {
        if (attempt.Result is not { Succeeded: true } && !attempt.IsSkipped)
        {
            diagnostics.Add(attempt.ToDiagnostic(command));
        }
    }

    private static void AddParseFailureDiagnostic(
        ICollection<string> diagnostics,
        CommandAttempt attempt,
        bool parseSucceeded,
        string command)
    {
        if (attempt.Result is { Succeeded: true } && !parseSucceeded)
        {
            diagnostics.Add($"{command} 回傳成功，但輸出格式無法辨識。");
        }
    }

    private static DotNetEnvironmentSnapshot CreateFailedSnapshot(
        EnvironmentDetectionStatus dotNetStatus,
        bool toolManifestExists,
        IReadOnlyList<string> diagnostics) =>
        new(
            dotNetStatus,
            null,
            null,
            null,
            EnvironmentDetectionStatus.DetectionFailed,
            [],
            EnvironmentDetectionStatus.DetectionFailed,
            [],
            EnvironmentDetectionStatus.DetectionFailed,
            [],
            toolManifestExists ? EnvironmentDetectionStatus.Available : EnvironmentDetectionStatus.Missing,
            toolManifestExists ? EnvironmentDetectionStatus.DetectionFailed : EnvironmentDetectionStatus.Missing,
            [],
            Array.AsReadOnly(diagnostics.ToArray()));

    private static string? ParseActiveSdkVersion(IEnumerable<string> lines)
    {
        var inSdkSection = false;
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.Equals(".NET SDK:", StringComparison.Ordinal))
            {
                inSdkSection = true;
                continue;
            }

            if (inSdkSection && trimmed.StartsWith("Version:", StringComparison.Ordinal))
            {
                return trimmed["Version:".Length..].Trim();
            }

            if (inSdkSection && trimmed.StartsWith(".NET ", StringComparison.Ordinal))
            {
                break;
            }
        }

        return null;
    }

    private static string? ParseInfoValue(IEnumerable<string> lines, string section, string key)
    {
        var inSection = false;
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.Equals(section, StringComparison.Ordinal))
            {
                inSection = true;
                continue;
            }

            if (inSection && trimmed.StartsWith(key, StringComparison.Ordinal))
            {
                return trimmed[key.Length..].Trim();
            }

            if (inSection && trimmed.EndsWith(':'))
            {
                break;
            }
        }

        return null;
    }

    private static ParsedList<DotNetSdkInstallation> ParseSdks(IEnumerable<string> lines) => ParseRows(
        lines,
        @"^(?<version>\S+)\s+\[(?<path>.+)\]$",
        match => new DotNetSdkInstallation(match.Groups["version"].Value, match.Groups["path"].Value));

    private static ParsedList<DotNetRuntimeInstallation> ParseRuntimes(IEnumerable<string> lines) => ParseRows(
        lines,
        @"^(?<name>\S+)\s+(?<version>\S+)\s+\[(?<path>.+)\]$",
        match => new DotNetRuntimeInstallation(
            match.Groups["name"].Value,
            match.Groups["version"].Value,
            match.Groups["path"].Value));

    private static WorkloadParseResult ParseWorkloads(IEnumerable<string> lines)
    {
        var output = string.Join(Environment.NewLine, lines);
        var marker = "==workloadListJsonOutputStart==";
        var markerIndex = output.IndexOf(marker, StringComparison.Ordinal);
        var jsonStart = output.IndexOf('{', markerIndex >= 0 ? markerIndex + marker.Length : 0);
        if (jsonStart < 0)
        {
            return WorkloadParseResult.Failed;
        }

        try
        {
            var jsonBytes = Encoding.UTF8.GetBytes(output[jsonStart..]);
            var reader = new Utf8JsonReader(jsonBytes);
            while (reader.Read() && reader.TokenType != JsonTokenType.StartObject)
            {
            }

            if (reader.TokenType != JsonTokenType.StartObject)
            {
                return WorkloadParseResult.Failed;
            }

            using var document = JsonDocument.ParseValue(ref reader);
            if (!document.RootElement.TryGetProperty("installed", out var installed) ||
                installed.ValueKind != JsonValueKind.Array)
            {
                return WorkloadParseResult.Failed;
            }

            var versions = new Dictionary<string, string>(StringComparer.Ordinal);
            if (document.RootElement.TryGetProperty("updateAvailable", out var updates) &&
                updates.ValueKind == JsonValueKind.Array)
            {
                foreach (var update in updates.EnumerateArray())
                {
                    if (update.TryGetProperty("workloadId", out var id) &&
                        update.TryGetProperty("existingManifestVersion", out var version) &&
                        id.ValueKind == JsonValueKind.String && version.ValueKind == JsonValueKind.String)
                    {
                        versions[id.GetString()!] = version.GetString()!;
                    }
                }
            }

            var workloads = installed.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString()!)
                .Select(id => new DotNetWorkloadInstallation(id, versions.GetValueOrDefault(id)))
                .ToArray();
            return new WorkloadParseResult(true, workloads);
        }
        catch (JsonException)
        {
            return WorkloadParseResult.Failed;
        }
    }

    private static ParsedList<LocalDotNetTool> ParseTools(IEnumerable<string> lines)
    {
        var tools = new List<LocalDotNetTool>();
        foreach (var line in lines.Select(value => value.Trim()).Where(value => value.Length > 0))
        {
            if (line.StartsWith("Package Id", StringComparison.OrdinalIgnoreCase) || line.All(character => character == '-'))
            {
                continue;
            }

            var match = Regex.Match(line, @"^(?<id>\S+)\s+(?<version>\S+)\s+(?<commands>.+)$");
            if (!match.Success)
            {
                return ParsedList<LocalDotNetTool>.Failed;
            }

            tools.Add(new LocalDotNetTool(
                match.Groups["id"].Value,
                match.Groups["version"].Value,
                match.Groups["commands"].Value));
        }

        return new ParsedList<LocalDotNetTool>(true, tools.AsReadOnly());
    }

    private static ParsedList<T> ParseRows<T>(IEnumerable<string> lines, string pattern, Func<Match, T> create)
    {
        var rows = new List<T>();
        foreach (var line in lines.Select(value => value.Trim()).Where(value => value.Length > 0))
        {
            var match = Regex.Match(line, pattern);
            if (!match.Success)
            {
                return ParsedList<T>.Failed;
            }

            rows.Add(create(match));
        }

        return new ParsedList<T>(true, rows.AsReadOnly());
    }

    private sealed record CommandAttempt(CommandResult? Result, Exception? Exception, bool IsMissingExecutable, bool IsSkipped = false)
    {
        public static CommandAttempt Skipped { get; } = new(null, null, false, true);

        public string ToDiagnostic(string command)
        {
            if (Exception is not null)
            {
                return $"{command} 啟動失敗：{Exception.Message}";
            }

            var result = Result!;
            var output = string.Join(Environment.NewLine, result.StandardError);
            return string.IsNullOrWhiteSpace(output)
                ? $"{command} 失敗，結束碼 {result.ExitCode}。"
                : $"{command} 失敗，結束碼 {result.ExitCode}：{output}";
        }
    }

    private sealed record WorkloadParseResult(bool IsValid, IReadOnlyList<DotNetWorkloadInstallation> Workloads)
    {
        public static WorkloadParseResult Failed { get; } = new(false, []);
    }

    private sealed record ParsedList<T>(bool IsValid, IReadOnlyList<T> Items)
    {
        public static ParsedList<T> Failed { get; } = new(false, []);
    }
}
