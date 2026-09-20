# Gateway

The Aether Gateway is a small authentication and server-discovery boundary for external clients. A client proves its identity to the Gateway, and application-provided policy decides which real server endpoints that identity may receive.

The initial implementation deliberately does not proxy application traffic. After one authentication exchange, it returns a short-lived connection grant containing eligible endpoints and opaque access tickets, then closes the connection. Each real server is responsible for validating its ticket before accepting the client session.

## Protocol

The wire protocol uses protobuf messages from `src/Aether.Gateway.Protocol/Protos/gateway.proto`. Each message is framed as:

1. A four-byte unsigned payload length in network byte order.
2. Exactly that many bytes of protobuf binary payload.

Protocol version 1 accepts one `GatewayAuthenticationRequest` and returns one `GatewayAuthenticationResponse`. The request contains an authentication scheme, opaque credential bytes, an optional requested service, and untrusted routing attributes. The response contains either a grant or a stable rejection code.

Frame size, authentication time, concurrent connections, and granted endpoint count are bounded. TLS is required by default because protobuf serialization does not encrypt credentials. `AllowUnencryptedConnections` exists only for explicitly controlled development and testing environments.

## Application integration

Register an authenticator and route resolver with the .NET host:

```csharp
builder.Services.AddAetherGateway<MyAuthenticator, MyRouteResolver>(options =>
{
    options.ListenEndPoint = new IPEndPoint(IPAddress.Any, 7777);
    options.ServerCertificate = certificate;
});
```

`IAetherGatewayAuthenticator` validates credential bytes and returns a stable subject plus an authenticated `ClaimsPrincipal`. Implementations may use platform tokens, signed bearer tokens, device credentials, or another application-specific mechanism. The core does not select an identity provider.

`IAetherGatewayRouteResolver` receives only an authenticated identity. It applies authorization, placement, capacity, region, and availability policy, then returns zero or more `GatewayRoute` values. Every route must contain an opaque, short-lived access ticket. The real server must validate that ticket and bind it to the expected subject, server, service, and expiry; an endpoint alone is not authorization.

## Trust boundaries

- Treat credentials and request attributes as untrusted input.
- Never log credential bytes or access tickets.
- Keep authentication separate from route authorization: proving an identity does not imply access to every service.
- Mint tickets only after route authorization and make them audience-bound to the selected server or service.
- Prefer single-use tickets or explicit replay protection where the threat model requires it.
- Reject startup without a server certificate unless plaintext transport was explicitly enabled.

The framework currently defines the exchange and extension points; it does not provide a production identity provider, ticket signer, service registry, load balancer, or real-server ticket validator. Those policies should be introduced as separate modules when their concrete requirements are known.
