using CmdPalDockStats.Contracts;
using LibreHardwareMonitor.Hardware;

namespace CmdPalDockStats.SensorHost;

internal sealed class HardwareSampler : IDisposable
{
    private readonly Computer _computer = new()
    {
        IsCpuEnabled = true,
        IsGpuEnabled = true,
        IsMemoryEnabled = true,
        IsStorageEnabled = true,
        IsMotherboardEnabled = true,
        IsControllerEnabled = true,
        IsBatteryEnabled = true,
    };
    private readonly List<string> _startupDiagnostics = [];

    public HardwareSampler()
    {
        try
        {
            _computer.Open();
        }
        catch (Exception exception)
        {
            _startupDiagnostics.Add($"LHM initialization: {exception.GetType().Name}: {exception.Message}");
        }
    }

    public MetricSnapshot Sample()
    {
        var descriptors = new List<MetricDescriptor>();
        var samples = new List<MetricSample>();
        var diagnostics = new List<string>(_startupDiagnostics);
        foreach (var hardware in _computer.Hardware)
        {
            Visit(hardware, descriptors, samples, diagnostics);
        }

        if (!descriptors.Any(metric => metric.Kind.EndsWith(".temperature", StringComparison.Ordinal)))
        {
            diagnostics.Add("No temperature sensors reported. Check hardware support, privileges and the LHM/PawnIO requirements.");
        }

        return new(descriptors.ToArray(), samples.ToArray(), diagnostics.ToArray());
    }

    private static void Visit(IHardware hardware, List<MetricDescriptor> descriptors,
        List<MetricSample> samples, List<string> diagnostics)
    {
        Exception? failure = null;
        try
        {
            hardware.Update();
        }
        catch (Exception exception)
        {
            failure = exception;
            diagnostics.Add($"{hardware.HardwareType}: {exception.GetType().Name}: {exception.Message}");
        }

        foreach (var sensor in hardware.Sensors)
        {
            var unit = sensor.SensorType switch
            {
                SensorType.Temperature => "C",
                SensorType.Load or SensorType.Level or SensorType.Control => "%",
                SensorType.Fan => "RPM",
                SensorType.Voltage => "V",
                SensorType.Power => "W",
                SensorType.Clock => "MHz",
                SensorType.Data => "GiB",
                SensorType.SmallData => "MiB",
                SensorType.Throughput => "B/s",
                SensorType.Current => "A",
                _ => null,
            };
            if (unit is null || descriptors.Count >= SensorProtocol.MaxMetrics)
            {
                continue;
            }

            var category = hardware.HardwareType switch
            {
                HardwareType.Cpu => "cpu",
                HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel => "gpu",
                HardwareType.Storage => "disk",
                HardwareType.Memory => "ram",
                HardwareType.Battery => "battery",
                _ => "sensors",
            };
            var id = $"lhm{sensor.Identifier}";
            descriptors.Add(new(id, "LibreHardwareMonitor", hardware.Identifier.ToString(), sensor.Identifier.ToString(),
                $"{hardware.Name} / {sensor.Name}", $"{category}.{sensor.SensorType.ToString().ToLowerInvariant()}", unit,
                $"LHM 0.9.6 / {hardware.HardwareType} / {sensor.SensorType} / {sensor.Name}"));
            double? value = failure is null && sensor.Value is float reading && float.IsFinite(reading) ? reading : null;
            samples.Add(new(id, value, DateTimeOffset.UtcNow,
                failure is not null ? MetricStatus.Error : value.HasValue ? MetricStatus.Available : MetricStatus.Unavailable,
                failure is not null ? "hardware-update-failed" : value.HasValue ? null : "sensor-no-reading"));
        }

        foreach (var child in hardware.SubHardware)
        {
            Visit(child, descriptors, samples, diagnostics);
        }
    }

    public void Dispose() => _computer.Close();
}