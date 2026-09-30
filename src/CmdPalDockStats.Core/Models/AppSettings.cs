using CmdPalDockStats.Contracts;

namespace CmdPalDockStats.Core.Models;

public sealed record DisplaySlot(string Id, string MetricId, string Label, int Order, string? Unit = null,
    MetricDescriptor? LastKnownMetric = null);

public sealed record AppSettings
{
    public const int CurrentSchemaVersion = 1;
    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
    public int SystemIntervalMs { get; init; } = 1000;
    public int SensorIntervalMs { get; init; } = 2000;
    public string TemperatureUnit { get; init; } = "C";
    public string RateUnit { get; init; } = "MiB/s";
    public DisplaySlot[] Slots { get; init; } =
    [
        new("cpu-load", "windows/cpu/usage", "CPU", 0),
        new("ram-used", "windows/memory/physical-used-percent", "RAM", 1),
    ];

    public void Validate()
    {
        if (SchemaVersion != CurrentSchemaVersion)
        {
            throw new InvalidDataException("Unsupported settings schema. Writing is disabled.");
        }

        if (SystemIntervalMs is not (1000 or 2000 or 5000) || SensorIntervalMs is not (2000 or 5000 or 10000) ||
            TemperatureUnit is not ("C" or "F") || RateUnit is not ("MiB/s" or "Mbps"))
        {
            throw new InvalidDataException("Invalid sampling interval or display unit.");
        }

        if (Slots is null || Slots.Length > 32 || Slots.Any(slot => slot is null ||
            string.IsNullOrWhiteSpace(slot.Id) || string.IsNullOrWhiteSpace(slot.MetricId) ||
            string.IsNullOrWhiteSpace(slot.Label) || slot.Label.Length > 80 || slot.Order < 0 ||
            slot.Unit is not (null or "C" or "F" or "MiB/s" or "Mbps")) ||
            Slots.Select(slot => slot.Id).Distinct(StringComparer.Ordinal).Count() != Slots.Length)
        {
            throw new InvalidDataException("Invalid display slots.");
        }
    }
}