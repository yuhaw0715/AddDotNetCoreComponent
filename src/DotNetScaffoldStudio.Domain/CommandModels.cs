using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;

namespace DotNetScaffoldStudio.Domain;

public enum CommandCancellationPolicy
{
    GracefulThenKillTree
}

public enum CommandOutputStream
{
    StandardOutput,
    StandardError
}

public sealed record CommandOutputLine(CommandOutputStream Stream, string Text);

public sealed record CommandArgument
{
    public CommandArgument(string value, bool isSecret = false)
    {
        ArgumentNullException.ThrowIfNull(value);
        Value = value;
        IsSecret = isSecret;
    }

    public string Value { get; }
    public bool IsSecret { get; }
}

public sealed record CommandRequest
{
    public CommandRequest(
        string executable,
        IReadOnlyList<CommandArgument> arguments,
        string workingDirectory,
        FeatureRisk risk,
        bool modifiesWorkspace,
        IReadOnlyDictionary<string, string>? environment = null,
        IReadOnlyList<string>? expectedOutputs = null,
        CommandCancellationPolicy cancellationPolicy = CommandCancellationPolicy.GracefulThenKillTree)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executable);
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);
        if (!Path.IsPathFullyQualified(workingDirectory))
        {
            throw new ArgumentException("工作目錄必須是絕對路徑。", nameof(workingDirectory));
        }

        var environmentCopy = environment is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new Dictionary<string, string>(environment, StringComparer.Ordinal);
        if (environmentCopy.Keys.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("環境變數名稱不得空白。", nameof(environment));
        }

        Executable = executable;
        Arguments = Array.AsReadOnly(arguments.ToArray());
        WorkingDirectory = Path.GetFullPath(workingDirectory);
        Risk = risk;
        ModifiesWorkspace = modifiesWorkspace;
        Environment = new ReadOnlyDictionary<string, string>(environmentCopy);
        ExpectedOutputs = Array.AsReadOnly(expectedOutputs?.ToArray() ?? []);
        CancellationPolicy = cancellationPolicy;
    }

    public string Executable { get; }
    public IReadOnlyList<CommandArgument> Arguments { get; }
    public string WorkingDirectory { get; }
    public FeatureRisk Risk { get; }
    public bool ModifiesWorkspace { get; }
    public IReadOnlyDictionary<string, string> Environment { get; }
    public IReadOnlyList<string> ExpectedOutputs { get; }
    public CommandCancellationPolicy CancellationPolicy { get; }

    public string GetConfirmationFingerprint()
    {
        var content = string.Join('\u001f',
        [
            Executable,
            WorkingDirectory,
            Risk.ToString(),
            ModifiesWorkspace.ToString(),
            .. Arguments.Select(argument => argument.Value),
            .. Environment.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => $"{pair.Key}={pair.Value}")
        ]);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content)));
    }
}
