using DotNetScaffoldStudio.Application;

namespace DotNetScaffoldStudio.Infrastructure;

public sealed class UserInitiatedNetworkAccessAdapter : INetworkAccessAdapter
{
    public Task<NetworkAccessDecision> AuthorizeAsync(
        NetworkAccessRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(request.UserInitiated
            ? new NetworkAccessDecision(true)
            : new NetworkAccessDecision(false, "網路操作不是由使用者明確觸發。"));
    }
}
