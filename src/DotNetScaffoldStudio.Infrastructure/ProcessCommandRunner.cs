using System.Diagnostics;
using DotNetScaffoldStudio.Application;
using DotNetScaffoldStudio.Domain;

namespace DotNetScaffoldStudio.Infrastructure;

public sealed class ProcessCommandRunner : ICommandRunner
{
    private static readonly TimeSpan GracefulShutdownTimeout = TimeSpan.FromMilliseconds(750);

    public async Task<CommandResult> RunAsync(
        CommandRequest request,
        IProgress<CommandOutputLine>? progress,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(request.WorkingDirectory))
        {
            throw new DirectoryNotFoundException($"找不到工作目錄：{request.WorkingDirectory}");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = request.Executable,
            WorkingDirectory = request.WorkingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        foreach (var argument in request.Arguments)
        {
            startInfo.ArgumentList.Add(argument.Value);
        }

        startInfo.Environment.Clear();
        foreach (var variable in request.Environment)
        {
            startInfo.Environment.Add(variable.Key, variable.Value);
        }

        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        var startedAt = DateTimeOffset.UtcNow;
        if (!process.Start())
        {
            throw new InvalidOperationException($"無法啟動可執行檔：{request.Executable}");
        }

        var redactor = new SensitiveDataRedactor(request);
        var stdout = new List<string>();
        var stderr = new List<string>();
        var stdoutTask = DrainAsync(process.StandardOutput, CommandOutputStream.StandardOutput, stdout, redactor, progress);
        var stderrTask = DrainAsync(process.StandardError, CommandOutputStream.StandardError, stderr, redactor, progress);
        var waitTask = process.WaitForExitAsync(CancellationToken.None);
        var wasCancelled = false;

        try
        {
            await waitTask.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            wasCancelled = true;
            if (!process.HasExited)
            {
                _ = process.CloseMainWindow();
                var completed = await Task.WhenAny(waitTask, Task.Delay(GracefulShutdownTimeout, CancellationToken.None));
                if (completed != waitTask && !process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }

            await waitTask;
        }

        await Task.WhenAll(stdoutTask, stderrTask);
        return new CommandResult(
            process.ExitCode,
            startedAt,
            DateTimeOffset.UtcNow,
            stdout,
            stderr,
            wasCancelled);
    }

    private static async Task DrainAsync(
        StreamReader reader,
        CommandOutputStream stream,
        ICollection<string> destination,
        SensitiveDataRedactor redactor,
        IProgress<CommandOutputLine>? progress)
    {
        while (await reader.ReadLineAsync() is { } line)
        {
            var safeLine = redactor.Redact(line);
            destination.Add(safeLine);
            progress?.Report(new CommandOutputLine(stream, safeLine));
        }
    }
}
