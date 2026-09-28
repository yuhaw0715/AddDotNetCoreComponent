using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace DotNetScaffoldStudio.App;

public static class MacOsDisplayLinkReadiness
{
    public delegate int DisplayLinkFactory(out IntPtr displayLink);

    public delegate void DisplayLinkReleaser(IntPtr displayLink);

    public static void WaitForReady(CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsMacOS())
        {
            return;
        }

        WaitForReady(
            CreateDisplayLink,
            ReleaseDisplayLink,
            TimeSpan.FromSeconds(1),
            cancellationToken);
    }

    public static void WaitForReady(
        DisplayLinkFactory createDisplayLink,
        DisplayLinkReleaser releaseDisplayLink,
        TimeSpan retryDelay,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(createDisplayLink);
        ArgumentNullException.ThrowIfNull(releaseDisplayLink);
        if (retryDelay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(retryDelay));
        }

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = createDisplayLink(out var displayLink);
            if (result == 0)
            {
                if (displayLink != IntPtr.Zero)
                {
                    releaseDisplayLink(displayLink);
                }

                return;
            }

            cancellationToken.WaitHandle.WaitOne(retryDelay);
        }
    }

    [SupportedOSPlatform("macos")]
    [DllImport(
        "/System/Library/Frameworks/CoreVideo.framework/CoreVideo",
        EntryPoint = "CVDisplayLinkCreateWithActiveCGDisplays")]
    private static extern int CreateDisplayLink(out IntPtr displayLink);

    [SupportedOSPlatform("macos")]
    [DllImport(
        "/System/Library/Frameworks/CoreVideo.framework/CoreVideo",
        EntryPoint = "CVDisplayLinkRelease")]
    private static extern void ReleaseDisplayLink(IntPtr displayLink);
}
