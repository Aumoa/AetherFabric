using System.Buffers.Binary;
using Google.Protobuf;

namespace Aether.Gateway.Protocol;

/// <summary>
/// Reads and writes protobuf messages with a four-byte, big-endian length prefix.
/// </summary>
public static class GatewayFrameCodec
{
    public static async ValueTask<T?> ReadAsync<T>(
        Stream stream,
        MessageParser<T> parser,
        int maximumFrameSize = GatewayProtocol.DefaultMaximumFrameSize,
        CancellationToken cancellationToken = default)
        where T : class, IMessage<T>
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(parser);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumFrameSize);

        var prefix = new byte[sizeof(uint)];
        var firstByteCount = await stream.ReadAsync(prefix.AsMemory(0, 1), cancellationToken);
        if (firstByteCount == 0)
        {
            return null;
        }

        await stream.ReadExactlyAsync(prefix.AsMemory(1), cancellationToken);
        var payloadLength = BinaryPrimitives.ReadUInt32BigEndian(prefix);
        if (payloadLength == 0 || payloadLength > maximumFrameSize)
        {
            throw new InvalidDataException(
                $"Gateway frame length {payloadLength} is outside the allowed range 1..{maximumFrameSize}.");
        }

        var payload = GC.AllocateUninitializedArray<byte>(checked((int)payloadLength));
        await stream.ReadExactlyAsync(payload, cancellationToken);

        try
        {
            return parser.ParseFrom(payload);
        }
        catch (InvalidProtocolBufferException exception)
        {
            throw new InvalidDataException("The gateway frame does not contain a valid protobuf message.", exception);
        }
    }

    public static async ValueTask WriteAsync<T>(
        Stream stream,
        T message,
        int maximumFrameSize = GatewayProtocol.DefaultMaximumFrameSize,
        CancellationToken cancellationToken = default)
        where T : class, IMessage<T>
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(message);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumFrameSize);

        var payload = message.ToByteArray();
        if (payload.Length == 0 || payload.Length > maximumFrameSize)
        {
            throw new InvalidDataException(
                $"Gateway frame length {payload.Length} is outside the allowed range 1..{maximumFrameSize}.");
        }

        var prefix = new byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32BigEndian(prefix, checked((uint)payload.Length));

        await stream.WriteAsync(prefix, cancellationToken);
        await stream.WriteAsync(payload, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }
}
