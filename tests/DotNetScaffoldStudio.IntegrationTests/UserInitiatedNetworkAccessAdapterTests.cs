using DotNetScaffoldStudio.Application;
using DotNetScaffoldStudio.Infrastructure;

namespace DotNetScaffoldStudio.IntegrationTests;

public sealed class UserInitiatedNetworkAccessAdapterTests
{
    [Fact]
    public async Task AuthorizeAsync_DoesNotAuthorizeBackgroundOperation()
    {
        var adapter = new UserInitiatedNetworkAccessAdapter();

        var result = await adapter.AuthorizeAsync(
            new NetworkAccessRequest("background-check", UserInitiated: false),
            CancellationToken.None);

        Assert.False(result.IsAllowed);
        Assert.NotNull(result.Diagnostic);
    }
}
