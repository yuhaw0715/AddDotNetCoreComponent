using System.Text.Json;
using DotNetScaffoldStudio.Domain;

namespace DotNetScaffoldStudio.Application;

public interface IExecutionHistory
{
    IReadOnlyList<ExecutionHistoryEntry> Entries { get; }

    void Record(
        CommandRequest request,
        CommandExecutionResult execution,
        string? feature = null,
        string? resultSummary = null);

    void RecordPreview(
        string feature,
        string maskedCommand,
        string workingDirectory,
        DateTimeOffset startedAt,
        DateTimeOffset completedAt,
        int? exitCode,
        bool wasCancelled,
        string resultSummary,
        IReadOnlyList<CommandOutputLine>? output = null);

    string Serialize();
}

public sealed class SessionExecutionHistory : IExecutionHistory
{
    private readonly object _gate = new();
    private readonly List<ExecutionHistoryEntry> _entries = [];

    public IReadOnlyList<ExecutionHistoryEntry> Entries
    {
        get
        {
            lock (_gate)
            {
                return _entries.ToArray();
            }
        }
    }

    public void Record(
        CommandRequest request,
        CommandExecutionResult execution,
        string? feature = null,
        string? resultSummary = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(execution);

        var redactor = new SensitiveDataRedactor(request);
        var command = execution.Command;
        var output = command.StandardOutput
            .Select(line => new CommandOutputLine(CommandOutputStream.StandardOutput, redactor.Redact(line)))
            .Concat(command.StandardError.Select(line => new CommandOutputLine(CommandOutputStream.StandardError, redactor.Redact(line))))
            .ToArray();
        var summary = redactor.Redact(resultSummary ?? CreateResultSummary(command));

        Add(new ExecutionHistoryEntry(
            command.StartedAt,
            command.CompletedAt,
            feature ?? request.Executable,
            CommandPreviewFormatter.Format(request),
            request.WorkingDirectory,
            command.ExitCode,
            command.WasCancelled,
            summary,
            output));
    }

    public void RecordPreview(
        string feature,
        string maskedCommand,
        string workingDirectory,
        DateTimeOffset startedAt,
        DateTimeOffset completedAt,
        int? exitCode,
        bool wasCancelled,
        string resultSummary,
        IReadOnlyList<CommandOutputLine>? output = null)
    {
        Add(new ExecutionHistoryEntry(
            startedAt,
            completedAt,
            feature,
            maskedCommand,
            workingDirectory,
            exitCode,
            wasCancelled,
            resultSummary,
            output));
    }

    public string Serialize()
    {
        lock (_gate)
        {
            return JsonSerializer.Serialize(_entries);
        }
    }

    private void Add(ExecutionHistoryEntry entry)
    {
        lock (_gate)
        {
            _entries.Add(entry);
        }
    }

    private static string CreateResultSummary(CommandResult command) =>
        command.WasCancelled
            ? "cancelled"
            : command.Succeeded
                ? "succeeded"
                : $"failed;exit-code={command.ExitCode}";
}
