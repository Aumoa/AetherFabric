using System.Net;
using System.Net.Sockets;
using System.Security.Claims;
using Aether.Gateway.Protocol;
using Google.Protobuf;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Aether.Gateway.Tests;

public sealed class AetherGatewayServerTests
{
    [Fact]
    public async Task AuthenticatedIdentity_ReceivesOnlyResolvedServerGrant()
    {
        var authenticator = new TestAuthenticator(acceptCredential: true);
        var routeResolver = new TestRouteResolver();
        await using var server = CreateServer(authenticator, routeResolver);
        await server.StartAsync(CancellationToken.None);

        try
        {
            var response = await ExchangeAsync(server.BoundEndPoint!, CreateRequest("valid"));

            Assert.Equal(GatewayAuthenticationResponse.ResultOneofCase.Grant, response.ResultCase);
            Assert.Equal("account-42", response.Grant.Subject);
            var endpoint = Assert.Single(response.Grant.Endpoints);
            Assert.Equal("world-7", endpoint.ServerId);
            Assert.Equal("world", endpoint.Service);
            Assert.Equal("127.0.0.1", endpoint.Host);
            Assert.Equal(9000u, endpoint.Port);
            Assert.Equal("tcp", endpoint.Transport);
            Assert.Equal("signed-ticket", endpoint.AccessTicket.ToStringUtf8());
            Assert.Equal("account-42", routeResolver.LastSubject);
            Assert.Equal("world", routeResolver.LastRequestedService);
        }
        finally
        {
            await server.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task RejectedCredential_DoesNotReachRouteResolver()
    {
        var authenticator = new TestAuthenticator(acceptCredential: false);
        var routeResolver = new TestRouteResolver();
        await using var server = CreateServer(authenticator, routeResolver);
        await server.StartAsync(CancellationToken.None);

        try
        {
            var response = await ExchangeAsync(server.BoundEndPoint!, CreateRequest("invalid"));

            Assert.Equal(GatewayAuthenticationResponse.ResultOneofCase.Rejection, response.ResultCase);
            Assert.Equal(GatewayRejectionReason.AuthenticationFailed, response.Rejection.Reason);
            Assert.Equal("invalid_credentials", response.Rejection.Code);
            Assert.Null(routeResolver.LastSubject);
        }
        finally
        {
            await server.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task StartAsync_RequiresTlsUnlessUnencryptedTransportIsExplicitlyAllowed()
    {
        await using var server = new AetherGatewayServer(
            Options.Create(new AetherGatewayOptions
            {
                ListenEndPoint = new IPEndPoint(IPAddress.Loopback, 0)
            }),
            new TestAuthenticator(acceptCredential: true),
            new TestRouteResolver(),
            NullLogger<AetherGatewayServer>.Instance);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            server.StartAsync(CancellationToken.None));

        Assert.Contains("certificate", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static AetherGatewayServer CreateServer(
        IAetherGatewayAuthenticator authenticator,
        IAetherGatewayRouteResolver routeResolver)
    {
        return new AetherGatewayServer(
            Options.Create(new AetherGatewayOptions
            {
                ListenEndPoint = new IPEndPoint(IPAddress.Loopback, 0),
                AllowUnencryptedConnections = true
            }),
            authenticator,
            routeResolver,
            NullLogger<AetherGatewayServer>.Instance);
    }

    private static GatewayAuthenticationRequest CreateRequest(string credential)
    {
        return new GatewayAuthenticationRequest
        {
            ProtocolVersion = GatewayProtocol.CurrentVersion,
            AuthenticationScheme = "test",
            Credential = ByteString.CopyFromUtf8(credential),
            RequestedService = "world"
        };
    }

    private static async Task<GatewayAuthenticationResponse> ExchangeAsync(
        IPEndPoint endpoint,
        GatewayAuthenticationRequest request)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(endpoint, CancellationToken.None);
        await using var stream = client.GetStream();

        await GatewayFrameCodec.WriteAsync(
            stream,
            request,
            cancellationToken: CancellationToken.None);
        var response = await GatewayFrameCodec.ReadAsync(
            stream,
            GatewayAuthenticationResponse.Parser,
            cancellationToken: CancellationToken.None);

        return Assert.IsType<GatewayAuthenticationResponse>(response);
    }

    private sealed class TestAuthenticator(bool acceptCredential) : IAetherGatewayAuthenticator
    {
        public ValueTask<GatewayAuthenticationResult> AuthenticateAsync(
            GatewayAuthenticationContext context,
            CancellationToken cancellationToken)
        {
            if (!acceptCredential)
            {
                return ValueTask.FromResult(GatewayAuthenticationResult.Rejected());
            }

            var identity = new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, "account-42")],
                authenticationType: "test");
            return ValueTask.FromResult(GatewayAuthenticationResult.Success(
                new GatewayAuthenticatedIdentity("account-42", new ClaimsPrincipal(identity))));
        }
    }

    private sealed class TestRouteResolver : IAetherGatewayRouteResolver
    {
        public string? LastSubject { get; private set; }

        public string? LastRequestedService { get; private set; }

        public ValueTask<IReadOnlyList<GatewayRoute>> ResolveAsync(
            GatewayRouteContext context,
            CancellationToken cancellationToken)
        {
            LastSubject = context.Identity.Subject;
            LastRequestedService = context.RequestedService;

            IReadOnlyList<GatewayRoute> routes =
            [
                new GatewayRoute(
                    "world-7",
                    "world",
                    "127.0.0.1",
                    9000,
                    "tcp",
                    "signed-ticket"u8.ToArray(),
                    DateTimeOffset.UtcNow.AddMinutes(1))
            ];
            return ValueTask.FromResult(routes);
        }
    }
}
