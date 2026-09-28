using DotNetScaffoldStudio.App;

namespace DotNetScaffoldStudio.UiTests;

public sealed class MacOsDisplayLinkReadinessTests
{
    [Fact]
    public void WaitForReady_RetriesUntilDisplayLinkIsAvailableAndReleasesIt()
    {
        var attempts = 0;
        var releasedHandle = IntPtr.Zero;

        MacOsDisplayLinkReadiness.WaitForReady(
            (out IntPtr displayLink) =>
            {
                attempts++;
                displayLink = attempts == 2 ? new IntPtr(42) : IntPtr.Zero;
                return attempts == 2 ? 0 : -6661;
            },
            displayLink => releasedHandle = displayLink,
            TimeSpan.Zero,
            CancellationToken.None);

        Assert.Equal(2, attempts);
        Assert.Equal(new IntPtr(42), releasedHandle);
    }

    [Fact]
    public async Task WaitForReady_HonorsCancellationWhileWaitingForDisplay()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(20));

        await Assert.ThrowsAsync<OperationCanceledException>(() => Task.Run(
            () => MacOsDisplayLinkReadiness.WaitForReady(
                (out IntPtr displayLink) =>
                {
                    displayLink = IntPtr.Zero;
                    return -6661;
                },
                _ => { },
                TimeSpan.FromSeconds(1),
                cancellation.Token)));
    }
}
