using System.Runtime.InteropServices;
using CmdPalDockStats.Contracts;

namespace CmdPalDockStats.Windows.Providers;

internal sealed class PdhSampler : IDisposable
{
    private const uint MoreData = 0x800007D2;
    private const uint DoubleFormat = 0x200;
    private readonly Dictionary<string, IntPtr> _counters = new();
    private readonly List<string> _diagnostics = [];
    private IntPtr _query;
    private bool _warmedUp;
    private readonly System.Diagnostics.Stopwatch _clock = System.Diagnostics.Stopwatch.StartNew();
    private TimeSpan _lastCollection;

    public PdhSampler()
    {
        var result = PdhOpenQuery(null, IntPtr.Zero, out _query);
        if (result != 0)
        {
            _diagnostics.Add($"PDH initialization: 0x{result:X8}");
            return;
        }

        Add("cpu", @"\Processor(_Total)\% Processor Time");
        Add("disk.read", @"\PhysicalDisk(*)\Disk Read Bytes/sec");
        Add("disk.write", @"\PhysicalDisk(*)\Disk Write Bytes/sec");
    }

    private void Add(string key, string path)
    {
        var result = PdhAddEnglishCounter(_query, path, IntPtr.Zero, out var counter);
        if (result == 0)
        {
            _counters[key] = counter;
        }
        else
        {
            _diagnostics.Add($"PDH {key}: 0x{result:X8}");
        }
    }

    public void Sample(List<MetricDescriptor> descriptors, List<MetricSample> samples, List<string> diagnostics)
    {
        diagnostics.AddRange(_diagnostics);
        var cpu = new MetricDescriptor("windows/cpu/usage", "Windows", "cpu-total", "processor-time",
            "CPU usage", "cpu.load", "%", "PDH / Processor(_Total) / % Processor Time");
        descriptors.Add(cpu);
        var result = _query != IntPtr.Zero ? PdhCollectQueryData(_query) : 1u;
        var ready = _warmedUp && _clock.Elapsed - _lastCollection <= TimeSpan.FromSeconds(30);
        _lastCollection = _clock.Elapsed;
        _warmedUp = result == 0;
        if (_counters.TryGetValue("cpu", out var counter) && result == 0)
        {
            var status = PdhGetFormattedCounterValue(counter, DoubleFormat, out _, out var value);
            samples.Add(CreateSample(cpu.Id, ready, status, value));
        }
        else
        {
            samples.Add(new(cpu.Id, null, DateTimeOffset.UtcNow, MetricStatus.Unavailable, $"pdh-0x{result:X8}"));
        }

        foreach (var key in new[] { "disk.read", "disk.write" })
        {
            if (!_counters.TryGetValue(key, out counter) || result != 0)
            {
                continue;
            }

            uint size = 0;
            uint count = 0;
            var status = PdhGetFormattedCounterArray(counter, DoubleFormat, ref size, ref count, IntPtr.Zero);
            if (status != MoreData || size == 0 || size > 1024 * 1024)
            {
                continue;
            }

            var buffer = Marshal.AllocHGlobal((int)size);
            try
            {
                status = PdhGetFormattedCounterArray(counter, DoubleFormat, ref size, ref count, buffer);
                if (status != 0 || count > size / Marshal.SizeOf<CounterItem>())
                {
                    diagnostics.Add($"PDH {key} array: 0x{status:X8}");
                    continue;
                }

                for (var index = 0; index < count; index++)
                {
                    var item = Marshal.PtrToStructure<CounterItem>(IntPtr.Add(buffer, index * Marshal.SizeOf<CounterItem>()));
                    var instance = Marshal.PtrToStringUni(item.Name);
                    if (string.IsNullOrEmpty(instance) || instance == "_Total")
                    {
                        continue;
                    }

                    var id = $"windows/{key}/{Uri.EscapeDataString(instance)}";
                    descriptors.Add(new(id, "Windows", $"physical-disk/{instance}", key,
                        $"Physical disk {instance} / {(key == "disk.read" ? "Read" : "Write")}", key, "B/s",
                        $"PDH / PhysicalDisk({instance}) / {key}; instance identity is not a device fingerprint"));
                    samples.Add(CreateSample(id, ready, 0, item.Value));
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
    }

    private static MetricSample CreateSample(string id, bool ready, uint status, CounterValue value)
    {
        if (!ready)
        {
            return new(id, null, DateTimeOffset.UtcNow, MetricStatus.WarmingUp, "initial-counter-sample");
        }

        var valid = status == 0 && value.Status <= 1 && double.IsFinite(value.Value) && value.Value >= 0;
        return new(id, valid ? value.Value : null, DateTimeOffset.UtcNow,
            valid ? MetricStatus.Available : MetricStatus.Unavailable, valid ? null : "invalid-pdh-counter");
    }

    public void Dispose()
    {
        if (_query != IntPtr.Zero)
        {
            PdhCloseQuery(_query);
            _query = IntPtr.Zero;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CounterValue { public uint Status; public double Value; }
    [StructLayout(LayoutKind.Sequential)]
    private struct CounterItem { public IntPtr Name; public CounterValue Value; }

    [DllImport("pdh.dll", CharSet = CharSet.Unicode, EntryPoint = "PdhOpenQueryW")]
    private static extern uint PdhOpenQuery(string? source, IntPtr userData, out IntPtr query);
    [DllImport("pdh.dll", CharSet = CharSet.Unicode, EntryPoint = "PdhAddEnglishCounterW")]
    private static extern uint PdhAddEnglishCounter(IntPtr query, string path, IntPtr userData, out IntPtr counter);
    [DllImport("pdh.dll")]
    private static extern uint PdhCollectQueryData(IntPtr query);
    [DllImport("pdh.dll")]
    private static extern uint PdhGetFormattedCounterValue(IntPtr counter, uint format, out uint type, out CounterValue value);
    [DllImport("pdh.dll", EntryPoint = "PdhGetFormattedCounterArrayW")]
    private static extern uint PdhGetFormattedCounterArray(IntPtr counter, uint format, ref uint size, ref uint count, IntPtr buffer);
    [DllImport("pdh.dll")]
    private static extern uint PdhCloseQuery(IntPtr query);
}