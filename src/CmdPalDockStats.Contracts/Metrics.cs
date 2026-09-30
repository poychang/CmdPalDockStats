namespace CmdPalDockStats.Contracts;

public enum MetricStatus
{
    Available, WarmingUp, Unavailable, PermissionRequired, DependencyMissing, Blocked, Stale, Error,
}

public sealed record MetricDescriptor(
    string Id, string Provider, string DeviceKey, string SensorKey,
    string Name, string Kind, string Unit, string Source);

public sealed record MetricSample(
    string MetricId, double? Value, DateTimeOffset TimestampUtc,
    MetricStatus Status, string? ReasonCode = null);

public sealed record MetricSnapshot(
    MetricDescriptor[] Descriptors, MetricSample[] Samples, string[] Diagnostics)
{
    public static MetricSnapshot Empty { get; } = new([], [], []);
}