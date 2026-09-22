using System.Diagnostics;
using DotNetScaffoldStudio.Application;
using DotNetScaffoldStudio.Domain;

namespace DotNetScaffoldStudio.Infrastructure;

public sealed class FinderRevealService : IFileRevealService
{
    public async Task<FileRevealResult> RevealAsync(string path, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!OperatingSystem.IsMacOS())
        {
            return new FileRevealResult(false, "Finder 開啟動作只支援 macOS。");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = "open",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.ArgumentList.Add("-R");
        startInfo.ArgumentList.Add(path);

        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
        {
            return new FileRevealResult(false, "無法啟動 Finder。");
        }

        await process.WaitForExitAsync(cancellationToken);
        return process.ExitCode == 0
            ? new FileRevealResult(true, "已要求 Finder 顯示輸出位置。")
            : new FileRevealResult(false, "Finder 無法顯示輸出位置。");
    }
}
