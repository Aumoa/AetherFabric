using System.Net;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using Aether.Gateway.Protocol;

namespace Aether.Gateway;

public sealed class AetherGatewayOptions
{
    public IPEndPoint ListenEndPoint { get; set; } = new(IPAddress.Loopback, 7777);

    public int ListenBacklog { get; set; } = 512;

    public int MaximumConcurrentConnections { get; set; } = 1024;

    public int MaximumFrameSize { get; set; } = GatewayProtocol.DefaultMaximumFrameSize;

    public int MaximumGrantedEndpoints { get; set; } = 32;

    public TimeSpan AuthenticationTimeout { get; set; } = TimeSpan.FromSeconds(10);

    public X509Certificate2? ServerCertificate { get; set; }

    public bool AllowUnencryptedConnections { get; set; }

    public SslProtocols EnabledSslProtocols { get; set; } = SslProtocols.Tls12 | SslProtocols.Tls13;

    internal void Validate()
    {
        ArgumentNullException.ThrowIfNull(ListenEndPoint);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ListenBacklog);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaximumConcurrentConnections);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaximumFrameSize);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaximumGrantedEndpoints);
        if (AuthenticationTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(AuthenticationTimeout),
                AuthenticationTimeout,
                "The authentication timeout must be positive.");
        }

        if (ServerCertificate is null && !AllowUnencryptedConnections)
        {
            throw new InvalidOperationException(
                "A gateway server certificate is required unless unencrypted connections are explicitly enabled.");
        }
    }
}
