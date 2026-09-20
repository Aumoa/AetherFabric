namespace Aether.Gateway;

public sealed record GatewayRouteContext(
    GatewayAuthenticatedIdentity Identity,
    string RequestedService,
    IReadOnlyDictionary<string, string> Attributes);

public interface IAetherGatewayRouteResolver
{
    ValueTask<IReadOnlyList<GatewayRoute>> ResolveAsync(
        GatewayRouteContext context,
        CancellationToken cancellationToken);
}

public sealed record GatewayRoute(
    string ServerId,
    string Service,
    string Host,
    int Port,
    string Transport,
    ReadOnlyMemory<byte> AccessTicket,
    DateTimeOffset ExpiresAt,
    IReadOnlyDictionary<string, string>? Metadata = null);
