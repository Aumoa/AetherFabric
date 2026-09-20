using System.Buffers.Binary;
using Aether.Gateway.Protocol;
using Google.Protobuf;

namespace Aether.Gateway.Tests;

public sealed class GatewayFrameCodecTests
{
    [Fact]
    public async Task RoundTrip_PreservesProtobufMessage()
    {
        var expected = new GatewayAuthenticationRequest
        {
            ProtocolVersion = GatewayProtocol.CurrentVersion,
            AuthenticationScheme = "bearer",
            Credential = ByteString.CopyFromUtf8("credential"),
            RequestedService = "world"
        };
        expected.Attributes.Add("region", "ap-northeast");

        await using var stream = new MemoryStream();
        await GatewayFrameCodec.WriteAsync(stream, expected);
        stream.Position = 0;

        var actual = await GatewayFrameCodec.ReadAsync(stream, GatewayAuthenticationRequest.Parser);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task ReadAsync_RejectsFrameLargerThanConfiguredLimit()
    {
        var prefix = new byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32BigEndian(prefix, 1024);
        await using var stream = new MemoryStream(prefix);

        var exception = await Assert.ThrowsAsync<InvalidDataException>(async () =>
            await GatewayFrameCodec.ReadAsync(
                stream,
                GatewayAuthenticationRequest.Parser,
                maximumFrameSize: 128));

        Assert.Contains("1024", exception.Message, StringComparison.Ordinal);
    }
}
