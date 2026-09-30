using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;
using CmdPalDockStats.Contracts;
using CmdPalDockStats.Core.Services;

namespace CmdPalDockStats.Windows.Providers;

public sealed class WindowsMetricProvider : IMetricProvider
{
    private readonly PdhSampler _pdh = new();
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly Dictionary<string, RateTracker> _rates = new();
    public string Name => "Windows";

    public Task<MetricSnapshot> SampleAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var descriptors = new List<MetricDescriptor>();
        var samples = new List<MetricSample>();
        var diagnostics = new List<string>();
        Run("PDH", () => _pdh.Sample(descriptors, samples, diagnostics), diagnostics);
        Run("Memory", () => Memory(descriptors, samples), diagnostics);
        Run("Network", () => Network(descriptors, samples, diagnostics), diagnostics);
        Run("Volumes", () => Volumes(descriptors, samples, diagnostics), diagnostics);
        Run("Battery", () => Battery(descriptors, samples), diagnostics);
        return Task.FromResult(new MetricSnapshot(descriptors.ToArray(), samples.ToArray(), diagnostics.ToArray()));
    }

    private static void Run(string source, Action action, List<string> diagnostics)
    {
        try { action(); }
        catch (Exception exception) { diagnostics.Add($"{source}: {exception.GetType().Name}: {exception.Message}"); }
    }

    private static void Add(List<MetricDescriptor> descriptors, List<MetricSample> samples,
        string id, string device, string name, string kind, string unit, string source, double? value,
        MetricStatus status = MetricStatus.Available, string? reason = null)
    {
        descriptors.Add(new(id, "Windows", device, id, name, kind, unit, source));
        samples.Add(new(id, value, DateTimeOffset.UtcNow, status, reason));
    }

    private static void Memory(List<MetricDescriptor> descriptors, List<MetricSample> samples)
    {
        var memory = new MemoryStatus { Length = (uint)Marshal.SizeOf<MemoryStatus>() };
        if (!GlobalMemoryStatusEx(ref memory))
        {
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        }

        var used = memory.TotalPhysical - memory.AvailablePhysical;
        const string source = "GlobalMemoryStatusEx / total physical - available physical";
        Add(descriptors, samples, "windows/memory/physical-used-percent", "physical-memory", "RAM used", "ram.load", "%",
            source, (double)used / memory.TotalPhysical * 100);
        Add(descriptors, samples, "windows/memory/physical-used", "physical-memory", "RAM used capacity", "ram.used", "GiB",
            source, used / 1073741824d);
        Add(descriptors, samples, "windows/memory/physical-total", "physical-memory", "RAM total capacity", "ram.total", "GiB",
            "GlobalMemoryStatusEx / total physical", memory.TotalPhysical / 1073741824d);
    }

    private void Network(List<MetricDescriptor> descriptors, List<MetricSample> samples, List<string> diagnostics)
    {
        var activeIds = new HashSet<string>();
        foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces().Where(adapter =>
            adapter.NetworkInterfaceType != NetworkInterfaceType.Loopback && adapter.OperationalStatus == OperationalStatus.Up))
        {
            Run($"Network {adapter.NetworkInterfaceType}", () =>
            {
                var statistics = adapter.GetIPStatistics();
                foreach (var (direction, bytes) in new[] { ("download", statistics.BytesReceived), ("upload", statistics.BytesSent) })
                {
                    var id = $"windows/network/{adapter.Id}/{direction}";
                    activeIds.Add(id);
                    if (!_rates.TryGetValue(id, out var tracker))
                    {
                        tracker = new RateTracker();
                        _rates[id] = tracker;
                    }

                    var rate = tracker.Next(bytes, _clock.Elapsed);
                    Add(descriptors, samples, id, adapter.Id, $"{adapter.Name} / {direction}", $"network.{direction}", "B/s",
                        $"NetworkInterface / {adapter.NetworkInterfaceType} / monotonic byte delta; VPN/virtual traffic may overlap",
                        rate, rate.HasValue ? MetricStatus.Available : MetricStatus.WarmingUp,
                        rate.HasValue ? null : "counter-baseline-reset");
                }
            }, diagnostics);
        }

        foreach (var id in _rates.Keys.Where(id => !activeIds.Contains(id)).ToArray())
        {
            _rates.Remove(id);
        }
    }

    private static void Volumes(List<MetricDescriptor> descriptors, List<MetricSample> samples, List<string> diagnostics)
    {
        foreach (var drive in DriveInfo.GetDrives().Where(drive => drive.DriveType == DriveType.Fixed && drive.IsReady))
        {
            Run("Volume capacity", () =>
            {
                var volume = new StringBuilder(128);
                if (!GetVolumeNameForVolumeMountPoint(drive.Name, volume, (uint)volume.Capacity))
                {
                    throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                }

                var device = volume.ToString();
                var prefix = $"windows/volume/{Uri.EscapeDataString(device)}";
                Add(descriptors, samples, prefix + "/used", device, $"{drive.Name} used capacity", "disk.capacity", "GiB",
                    "DriveInfo / total size - total free space; volume, not physical disk",
                    (drive.TotalSize - drive.TotalFreeSpace) / 1073741824d);
                Add(descriptors, samples, prefix + "/free", device, $"{drive.Name} free capacity", "disk.free", "GiB",
                    "DriveInfo / total free space; volume, not physical disk", drive.TotalFreeSpace / 1073741824d);
                Add(descriptors, samples, prefix + "/total", device, $"{drive.Name} total capacity", "disk.total", "GiB",
                    "DriveInfo / total size; volume, not physical disk", drive.TotalSize / 1073741824d);
            }, diagnostics);
        }
    }

    private static void Battery(List<MetricDescriptor> descriptors, List<MetricSample> samples)
    {
        if (!GetSystemPowerStatus(out var power))
        {
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        }

        const string source = "GetSystemPowerStatus";
        var absent = power.BatteryFlag != 255 && (power.BatteryFlag & 128) != 0;
        var known = !absent && power.BatteryLifePercent <= 100;
        Add(descriptors, samples, "windows/battery/percent", "system-battery", "Battery", "battery.level", "%", source,
            known ? power.BatteryLifePercent : null, known ? MetricStatus.Available : MetricStatus.Unavailable,
            absent ? "battery-not-present" : known ? null : "battery-unknown");
        Add(descriptors, samples, "windows/battery/ac", "system-power", "AC connected", "battery.ac", "bool", source,
            power.AcLineStatus <= 1 ? power.AcLineStatus : null,
            power.AcLineStatus <= 1 ? MetricStatus.Available : MetricStatus.Unavailable);
        Add(descriptors, samples, "windows/battery/charging", "system-battery", "Battery charging", "battery.charging", "bool", source,
            !absent && power.BatteryFlag != 255 ? ((power.BatteryFlag & 8) != 0 ? 1 : 0) : null,
            !absent && power.BatteryFlag != 255 ? MetricStatus.Available : MetricStatus.Unavailable);
        var lifetimeKnown = !absent && power.BatteryLifeTime != uint.MaxValue;
        Add(descriptors, samples, "windows/battery/remaining", "system-battery", "Battery remaining", "battery.remaining", "s", source,
            lifetimeKnown ? power.BatteryLifeTime : null, lifetimeKnown ? MetricStatus.Available : MetricStatus.Unavailable,
            lifetimeKnown ? null : "remaining-time-unknown");
    }

    public void Dispose() => _pdh.Dispose();

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatus
    {
        public uint Length, Load;
        public ulong TotalPhysical, AvailablePhysical, TotalPageFile, AvailablePageFile, TotalVirtual, AvailableVirtual, AvailableExtendedVirtual;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct PowerStatus
    {
        public byte AcLineStatus, BatteryFlag, BatteryLifePercent, SystemStatusFlag;
        public uint BatteryLifeTime, BatteryFullLifeTime;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatus memory);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemPowerStatus(out PowerStatus power);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetVolumeNameForVolumeMountPoint(string mountPoint, StringBuilder volumeName, uint length);
}