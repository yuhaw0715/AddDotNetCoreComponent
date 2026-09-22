using System.Diagnostics;
using DotNetScaffoldStudio.Application;
using DotNetScaffoldStudio.Domain;

namespace DotNetScaffoldStudio.Infrastructure;

public sealed class FinderRevealService : IFileRevealService
{
    private readonly IUiTextProvider _text;

    public FinderRevealService(IUiTextProvider? text = null)
    {
        _text = text ?? new DefaultUiTextProvider();
    }

    public async Task<FileRevealResult> RevealAsync(string path, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!OperatingSystem.IsMacOS())
        {
            return new FileRevealResult(false, _text.Get("FileReveal.UnsupportedPlatform"));
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
            return new FileRevealResult(false, _text.Get("FileReveal.StartFailure"));
        }

        await process.WaitForExitAsync(cancellationToken);
        return process.ExitCode == 0
            ? new FileRevealResult(true, _text.Get("FileReveal.Success"))
            : new FileRevealResult(false, _text.Get("FileReveal.Failure"));
    }
}
