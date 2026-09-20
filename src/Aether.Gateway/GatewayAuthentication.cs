using System.Net;
using System.Security.Claims;

namespace Aether.Gateway;

public sealed record GatewayAuthenticationContext(
    string Scheme,
    ReadOnlyMemory<byte> Credential,
    IReadOnlyDictionary<string, string> Attributes,
    EndPoint? RemoteEndPoint);

public interface IAetherGatewayAuthenticator
{
    ValueTask<GatewayAuthenticationResult> AuthenticateAsync(
        GatewayAuthenticationContext context,
        CancellationToken cancellationToken);
}

public sealed record GatewayAuthenticatedIdentity
{
    public GatewayAuthenticatedIdentity(string subject, ClaimsPrincipal principal)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        ArgumentNullException.ThrowIfNull(principal);
        if (principal.Identities.All(static identity => !identity.IsAuthenticated))
        {
            throw new ArgumentException("The principal must contain an authenticated identity.", nameof(principal));
        }

        Subject = subject;
        Principal = principal;
    }

    public string Subject { get; }

    public ClaimsPrincipal Principal { get; }
}

public sealed record GatewayAuthenticationResult
{
    private GatewayAuthenticationResult(
        GatewayAuthenticatedIdentity? identity,
        string failureCode)
    {
        Identity = identity;
        FailureCode = failureCode;
    }

    public bool Succeeded => Identity is not null;

    public GatewayAuthenticatedIdentity? Identity { get; }

    public string FailureCode { get; }

    public static GatewayAuthenticationResult Success(GatewayAuthenticatedIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        return new(identity, string.Empty);
    }

    public static GatewayAuthenticationResult Rejected(string failureCode = "invalid_credentials")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(failureCode);
        return new(null, failureCode);
    }
}
