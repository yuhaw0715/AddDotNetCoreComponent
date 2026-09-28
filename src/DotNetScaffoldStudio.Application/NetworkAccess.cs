namespace DotNetScaffoldStudio.Application;

public sealed record NetworkAccessRequest(string Operation, bool UserInitiated);

public sealed record NetworkAccessDecision(bool IsAllowed, string? Diagnostic = null);

public interface INetworkAccessAdapter
{
    Task<NetworkAccessDecision> AuthorizeAsync(
        NetworkAccessRequest request,
        CancellationToken cancellationToken);
}
