# ADR-0002: Gateway Transport and Trust Boundary

- Status: Accepted
- Date: 2026-09-20

## Context

Aether needs an external connection boundary where clients prove their identity and receive only the real server destinations they are authorized to use. The reference `RemoteDebugBackend` project demonstrates useful separation between an external Gateway and backend processing, but its SignalR, MessagePack, static session maps, and application-specific JWT flow are not appropriate as framework contracts.

The protocol must be usable by C# and native game clients on Windows, Linux, and macOS. Credential handling, route authorization, and access to real servers must remain separate concerns.

## Decision

Use a small TCP protocol carrying binary protobuf messages. Prefix each protobuf payload with a four-byte network-order length. Version the protobuf contract independently from public .NET APIs.

The Gateway performs one bounded authentication exchange:

1. The client sends an authentication scheme, opaque credential, requested service, and optional untrusted attributes.
2. An application-provided authenticator validates the credential and creates an authenticated identity.
3. An application-provided route resolver authorizes that identity and selects eligible real servers.
4. The Gateway returns endpoints with opaque, short-lived access tickets.
5. Each real server validates its ticket before creating a session.

TLS is required by default. Plain TCP is available only through an explicit development/testing option. The first implementation acts as an authentication and discovery broker, not a traffic proxy.

## Why TCP Instead of gRPC

This boundary is a single compact exchange that must be straightforward to implement in game and native clients. Length-prefixed protobuf over TCP keeps the wire format portable, avoids requiring an HTTP/2 and gRPC runtime in every client, and permits precise bounds and lifecycle behavior.

gRPC remains a strong candidate for trusted server-to-server control APIs, streaming administration, or environments that already standardize on HTTP/2. It should be reconsidered if browser support, generated RPC semantics, intermediaries, or standard observability become more important than the small client transport surface.

## Alternatives Considered

### Copy the SignalR gateway

Rejected because it carries application-specific hub methods and session state, uses a different serialization format, and couples the framework boundary to ASP.NET SignalR behavior.

### Use gRPC for the client boundary

Deferred because the initial exchange does not need RPC streaming or HTTP/2 semantics, while native client integration would become heavier.

### Proxy all real-server traffic through the Gateway

Deferred because it changes the Gateway into a high-throughput data plane, increases latency and operational scope, and is not required to establish authenticated server access. A proxy mode can be designed separately if hiding endpoints or centralized traffic enforcement becomes a concrete requirement.

## Consequences

- C# and C++ clients can share the same protobuf schema and simple framing contract.
- Authentication providers, authorization policy, service discovery, load balancing, and ticket signing remain replaceable.
- The protocol requires custom connection handling and explicit compatibility tests.
- Applications must supply production authentication and ticket-validation modules.
- Exposing real server endpoints is part of the current design; deployments that cannot do so will need a separately designed relay or proxy mode.
