using System.Globalization;
using CmdPalDockStats.Contracts;
using CmdPalDockStats.Core.Models;

namespace CmdPalDockStats.Core.Services;

public static class MetricFormatter
{
    public static MetricSample EffectiveSample(MetricSample sample, DateTimeOffset now, int intervalMs)
    {
        if (sample.Status == MetricStatus.Available && now - sample.TimestampUtc >
            TimeSpan.FromMilliseconds(Math.Max(intervalMs * 3L, 10000)))
        {
            return sample with { Status = MetricStatus.Stale, ReasonCode = "sample-expired" };
        }

        return sample;
    }

    public static string Format(MetricDescriptor? descriptor, MetricSample? sample, AppSettings settings, string? unitOverride = null)
    {
        if (descriptor is null)
        {
            return "Device not connected";
        }

        if (sample is null)
        {
            return "Warming up";
        }

        sample = EffectiveSample(sample, DateTimeOffset.UtcNow,
            descriptor.Provider == "Windows" ? settings.SystemIntervalMs : settings.SensorIntervalMs);
        if (sample.Status != MetricStatus.Available || sample.Value is not double value || !double.IsFinite(value))
        {
            return sample.Status switch
            {
                MetricStatus.WarmingUp => "Warming up",
                MetricStatus.Stale => "Stale",
                MetricStatus.PermissionRequired => "Permission required",
                MetricStatus.DependencyMissing => "Dependency missing",
                MetricStatus.Blocked => "Blocked",
                MetricStatus.Error => "Read error",
                _ => "Unavailable",
            };
        }

        var unit = descriptor.Unit;
        if (unit == "C")
        {
            unit = unitOverride is "C" or "F" ? unitOverride : settings.TemperatureUnit;
            value = unit == "F" ? value * 9 / 5 + 32 : value;
            return value.ToString("F1", CultureInfo.InvariantCulture) + "\u00b0" + unit;
        }

        if (unit == "B/s")
        {
            unit = unitOverride is "MiB/s" or "Mbps" ? unitOverride : settings.RateUnit;
            value = unit == "Mbps" ? value * 8 / 1000000 : value / 1048576;
        }

        if (unit == "bool")
        {
            return value == 1 ? "Yes" : "No";
        }

        if (unit == "s")
        {
            return $"{value / 60:F0} min";
        }

        return value.ToString(unit == "RPM" ? "F0" : "F1", CultureInfo.InvariantCulture) +
            (unit == "%" ? "%" : " " + unit);
    }
}