using System.Collections.Concurrent;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using Aether.Gateway.Protocol;
using Google.Protobuf;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aether.Gateway;

public sealed class AetherGatewayServer(
    IOptions<AetherGatewayOptions> options,
    IAetherGatewayAuthenticator authenticator,
    IAetherGatewayRouteResolver routeResolver,
    ILogger<AetherGatewayServer> logger) : IHostedService, IAsyncDisposable
{
    private readonly AetherGatewayOptions _options = options.Value;
    private readonly ConcurrentDictionary<long, Task> _connections = new();
    private readonly CancellationTokenSource _shutdown = new();
    private TcpListener? _listener;
    private SemaphoreSlim? _connectionSlots;
    private Task? _acceptLoop;
    private long _nextConnectionId;

    public IPEndPoint? BoundEndPoint { get; private set; }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _options.Validate();

        _connectionSlots = new SemaphoreSlim(
            _options.MaximumConcurrentConnections,
            _options.MaximumConcurrentConnections);
        _listener = new TcpListener(_options.ListenEndPoint);
        _listener.Start(_options.ListenBacklog);
        BoundEndPoint = (IPEndPoint)_listener.LocalEndpoint;
        _acceptLoop = AcceptConnectionsAsync(_shutdown.Token);

        logger.LogInformation(
            "Aether gateway listening on {EndPoint} with TLS {TlsState}.",
            BoundEndPoint,
            _options.ServerCertificate is null ? "disabled" : "enabled");

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _shutdown.CancelAsync();
        _listener?.Stop();

        if (_acceptLoop is not null)
        {
            await IgnoreExpectedShutdownAsync(_acceptLoop, cancellationToken);
        }

        var connections = _connections.Values.ToArray();
        if (connections.Length > 0)
        {
            await Task.WhenAll(connections).WaitAsync(cancellationToken);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _shutdown.CancelAsync();
        _listener?.Stop();
        _connectionSlots?.Dispose();
        _shutdown.Dispose();
    }

    private async Task AcceptConnectionsAsync(CancellationToken cancellationToken)
    {
        var listener = _listener ?? throw new InvalidOperationException("The gateway has not been started.");
        var connectionSlots = _connectionSlots ?? throw new InvalidOperationException("The gateway has not been started.");

        while (!cancellationToken.IsCancellationRequested)
        {
            await connectionSlots.WaitAsync(cancellationToken);

            TcpClient client;
            try
            {
                client = await listener.AcceptTcpClientAsync(cancellationToken);
            }
            catch
            {
                connectionSlots.Release();
                throw;
            }

            var connectionId = Interlocked.Increment(ref _nextConnectionId);
            var connectionTask = RunConnectionAsync(connectionId, client, connectionSlots, cancellationToken);
            if (!_connections.TryAdd(connectionId, connectionTask))
            {
                client.Dispose();
                connectionSlots.Release();
                throw new InvalidOperationException($"Duplicate gateway connection id {connectionId}.");
            }
        }
    }

    private async Task RunConnectionAsync(
        long connectionId,
        TcpClient client,
        SemaphoreSlim connectionSlots,
        CancellationToken stoppingToken)
    {
        await Task.Yield();

        try
        {
            await HandleConnectionAsync(client, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Gateway connection {ConnectionId} failed.", connectionId);
        }
        finally
        {
            client.Dispose();
            _connections.TryRemove(connectionId, out _);
            connectionSlots.Release();
        }
    }

    private async Task HandleConnectionAsync(TcpClient client, CancellationToken stoppingToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        timeout.CancelAfter(_options.AuthenticationTimeout);
        var cancellationToken = timeout.Token;

        await using var stream = await CreateTransportStreamAsync(client, cancellationToken);
        GatewayAuthenticationRequest? request;
        try
        {
            request = await GatewayFrameCodec.ReadAsync(
                stream,
                GatewayAuthenticationRequest.Parser,
                _options.MaximumFrameSize,
                cancellationToken);
        }
        catch (InvalidDataException)
        {
            await SendRejectionAsync(
                stream,
                GatewayRejectionReason.MalformedRequest,
                "malformed_request",
                cancellationToken);
            return;
        }

        if (request is null)
        {
            return;
        }

        if (request.ProtocolVersion != GatewayProtocol.CurrentVersion)
        {
            await SendRejectionAsync(
                stream,
                GatewayRejectionReason.UnsupportedProtocol,
                "unsupported_protocol",
                cancellationToken);
            return;
        }

        if (string.IsNullOrWhiteSpace(request.AuthenticationScheme) || request.Credential.IsEmpty)
        {
            await SendRejectionAsync(
                stream,
                GatewayRejectionReason.MalformedRequest,
                "missing_credentials",
                cancellationToken);
            return;
        }

        var remoteEndPoint = client.Client.RemoteEndPoint;
        var attributes = request.Attributes.ToDictionary(static pair => pair.Key, static pair => pair.Value);
        GatewayAuthenticationResult authentication;
        try
        {
            authentication = await authenticator.AuthenticateAsync(
                new GatewayAuthenticationContext(
                    request.AuthenticationScheme,
                    request.Credential.ToByteArray(),
                    attributes,
                    remoteEndPoint),
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Gateway authentication failed unexpectedly for {RemoteEndPoint}.", remoteEndPoint);
            await SendRejectionAsync(
                stream,
                GatewayRejectionReason.InternalError,
                "authentication_unavailable",
                cancellationToken);
            return;
        }

        if (!authentication.Succeeded)
        {
            logger.LogInformation(
                "Gateway authentication rejected for {RemoteEndPoint} with code {FailureCode}.",
                remoteEndPoint,
                authentication.FailureCode);
            await SendRejectionAsync(
                stream,
                GatewayRejectionReason.AuthenticationFailed,
                authentication.FailureCode,
                cancellationToken);
            return;
        }

        var identity = authentication.Identity!;
        IReadOnlyList<GatewayRoute> routes;
        try
        {
            routes = await routeResolver.ResolveAsync(
                new GatewayRouteContext(identity, request.RequestedService, attributes),
                cancellationToken);
        }
        catch (UnauthorizedAccessException)
        {
            await SendRejectionAsync(
                stream,
                GatewayRejectionReason.NotAuthorized,
                "not_authorized",
                cancellationToken);
            return;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(
                exception,
                "Gateway route resolution failed unexpectedly for subject {Subject}.",
                identity.Subject);
            await SendRejectionAsync(
                stream,
                GatewayRejectionReason.InternalError,
                "routing_unavailable",
                cancellationToken);
            return;
        }

        if (routes.Count == 0)
        {
            await SendRejectionAsync(
                stream,
                GatewayRejectionReason.ServerUnavailable,
                "no_eligible_server",
                cancellationToken);
            return;
        }

        if (routes.Count > _options.MaximumGrantedEndpoints)
        {
            logger.LogError(
                "Gateway route resolver returned {EndpointCount} endpoints for subject {Subject}, exceeding the limit {EndpointLimit}.",
                routes.Count,
                identity.Subject,
                _options.MaximumGrantedEndpoints);
            await SendRejectionAsync(
                stream,
                GatewayRejectionReason.InternalError,
                "invalid_route_set",
                cancellationToken);
            return;
        }

        var response = new GatewayAuthenticationResponse
        {
            ProtocolVersion = GatewayProtocol.CurrentVersion,
            Grant = new GatewayConnectionGrant { Subject = identity.Subject }
        };

        try
        {
            foreach (var route in routes)
            {
                response.Grant.Endpoints.Add(CreateEndpoint(route));
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(
                exception,
                "Gateway route resolver returned an invalid route for subject {Subject}.",
                identity.Subject);
            await SendRejectionAsync(
                stream,
                GatewayRejectionReason.InternalError,
                "invalid_route",
                cancellationToken);
            return;
        }

        await GatewayFrameCodec.WriteAsync(stream, response, _options.MaximumFrameSize, cancellationToken);
        logger.LogInformation(
            "Gateway granted {EndpointCount} endpoint(s) to subject {Subject} for service {RequestedService}.",
            routes.Count,
            identity.Subject,
            request.RequestedService);
    }

    private async ValueTask<Stream> CreateTransportStreamAsync(
        TcpClient client,
        CancellationToken cancellationToken)
    {
        Stream stream = client.GetStream();
        if (_options.ServerCertificate is null)
        {
            return stream;
        }

        var secureStream = new SslStream(stream, leaveInnerStreamOpen: false);
        await secureStream.AuthenticateAsServerAsync(
            new SslServerAuthenticationOptions
            {
                ServerCertificate = _options.ServerCertificate,
                ClientCertificateRequired = false,
                EnabledSslProtocols = _options.EnabledSslProtocols
            },
            cancellationToken);
        return secureStream;
    }

    private static GatewayServerEndpoint CreateEndpoint(GatewayRoute route)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(route.ServerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(route.Service);
        ArgumentException.ThrowIfNullOrWhiteSpace(route.Host);
        ArgumentOutOfRangeException.ThrowIfLessThan(route.Port, IPEndPoint.MinPort);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(route.Port, IPEndPoint.MaxPort);
        ArgumentException.ThrowIfNullOrWhiteSpace(route.Transport);
        if (route.AccessTicket.IsEmpty)
        {
            throw new ArgumentException("A gateway route must include an access ticket.", nameof(route));
        }

        var endpoint = new GatewayServerEndpoint
        {
            ServerId = route.ServerId,
            Service = route.Service,
            Host = route.Host,
            Port = checked((uint)route.Port),
            Transport = route.Transport,
            AccessTicket = ByteString.CopyFrom(route.AccessTicket.Span),
            ExpiresAtUnixTimeSeconds = route.ExpiresAt.ToUnixTimeSeconds()
        };

        if (route.Metadata is not null)
        {
            foreach (var pair in route.Metadata)
            {
                endpoint.Metadata.Add(pair.Key, pair.Value);
            }
        }

        return endpoint;
    }

    private async ValueTask SendRejectionAsync(
        Stream stream,
        GatewayRejectionReason reason,
        string code,
        CancellationToken cancellationToken)
    {
        var response = new GatewayAuthenticationResponse
        {
            ProtocolVersion = GatewayProtocol.CurrentVersion,
            Rejection = new GatewayRejection
            {
                Reason = reason,
                Code = code
            }
        };

        await GatewayFrameCodec.WriteAsync(stream, response, _options.MaximumFrameSize, cancellationToken);
    }

    private static async Task IgnoreExpectedShutdownAsync(Task task, CancellationToken cancellationToken)
    {
        try
        {
            await task.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
        catch (SocketException)
        {
        }
    }
}
