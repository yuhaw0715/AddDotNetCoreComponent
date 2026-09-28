namespace DotNetScaffoldStudio.Domain;

public sealed record ExecutionHistoryEntry
{
    public ExecutionHistoryEntry(
        DateTimeOffset startedAt,
        DateTimeOffset completedAt,
        string feature,
        string maskedCommand,
        string workingDirectory,
        int? exitCode,
        bool wasCancelled,
        string resultSummary,
        IReadOnlyList<CommandOutputLine>? output = null)
    {
        if (completedAt < startedAt)
        {
            throw new ArgumentException("完成時間不得早於開始時間。", nameof(completedAt));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(feature);
        ArgumentException.ThrowIfNullOrWhiteSpace(maskedCommand);
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);
        ArgumentNullException.ThrowIfNull(resultSummary);

        StartedAt = startedAt;
        CompletedAt = completedAt;
        Feature = feature;
        MaskedCommand = maskedCommand;
        WorkingDirectory = workingDirectory;
        ExitCode = exitCode;
        WasCancelled = wasCancelled;
        ResultSummary = resultSummary;
        Output = Array.AsReadOnly(output?.ToArray() ?? []);
    }

    public DateTimeOffset StartedAt { get; }
    public DateTimeOffset CompletedAt { get; }
    public string Feature { get; }
    public string MaskedCommand { get; }
    public string WorkingDirectory { get; }
    public int? ExitCode { get; }
    public bool WasCancelled { get; }
    public string ResultSummary { get; }
    public IReadOnlyList<CommandOutputLine> Output { get; }
}
