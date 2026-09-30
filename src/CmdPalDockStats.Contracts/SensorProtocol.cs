using System.Buffers.Binary;
using System.Text.Json;

namespace CmdPalDockStats.Contracts;

public sealed record SensorRequest(int Version, string Operation);
public sealed record SensorResponse(int Version, MetricSnapshot Snapshot);

public static class SensorProtocol
{
    public const int Version = 1;
    public const int MaxMessageBytes = 1024 * 1024;
    public const int MaxMetrics = 4096;
    public static JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web);

    public static async Task WriteAsync<T>(Stream stream, T message, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(message, JsonOptions);
        if (payload.Length > MaxMessageBytes)
        {
            throw new InvalidDataException("Message exceeds the protocol limit.");
        }

        var header = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(header, payload.Length);
        await stream.WriteAsync(header, cancellationToken);
        await stream.WriteAsync(payload, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    public static async Task<T> ReadAsync<T>(Stream stream, CancellationToken cancellationToken)
    {
        var header = new byte[sizeof(int)];
        await stream.ReadExactlyAsync(header, cancellationToken);
        var length = BinaryPrimitives.ReadInt32LittleEndian(header);
        if (length <= 0 || length > MaxMessageBytes)
        {
            throw new InvalidDataException("Invalid message length.");
        }

        var payload = new byte[length];
        await stream.ReadExactlyAsync(payload, cancellationToken);
        return JsonSerializer.Deserialize<T>(payload, JsonOptions)
            ?? throw new InvalidDataException("Empty message.");
    }

    public static void Validate(SensorRequest request)
    {
        if (request.Version != Version || request.Operation != "snapshot")
        {
            throw new InvalidDataException("Unsupported protocol version or operation.");
        }
    }

    public static void Validate(SensorResponse response)
    {
        if (response.Version != Version || response.Snapshot is null ||
            response.Snapshot.Descriptors is null || response.Snapshot.Samples is null ||
            response.Snapshot.Diagnostics is null ||
            response.Snapshot.Descriptors.Length > MaxMetrics || response.Snapshot.Samples.Length > MaxMetrics ||
            response.Snapshot.Descriptors.Any(metric => metric is null || string.IsNullOrWhiteSpace(metric.Id)) ||
            response.Snapshot.Samples.Any(sample => sample is null || string.IsNullOrWhiteSpace(sample.MetricId) ||
                (sample.Value.HasValue && !double.IsFinite(sample.Value.Value))))
        {
            throw new InvalidDataException("Invalid sensor snapshot.");
        }
    }
}