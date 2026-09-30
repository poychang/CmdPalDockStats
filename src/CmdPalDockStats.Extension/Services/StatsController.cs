using CmdPalDockStats.Contracts;
using CmdPalDockStats.Core.Models;
using CmdPalDockStats.Core.Services;
using CmdPalDockStats.Windows.Providers;
using CmdPalDockStats.Windows.Services;

namespace CmdPalDockStats.Extension.Services;

internal sealed class StatsController
{
    private readonly object _gate = new();
    private AppSettings _settings;
    public SettingsStore Store { get; }
    public MetricRuntime Runtime { get; }
    public AppSettings Settings => Volatile.Read(ref _settings);
    public event EventHandler? SettingsChanged;

    public StatsController()
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CmdPalDockStats", "settings.json");
        Store = new SettingsStore(path);
        _settings = Store.Load();
        Runtime = new MetricRuntime([new WindowsMetricProvider(), new SensorHostClient()], () => Settings);
    }

    public void Change(Func<AppSettings, AppSettings> change)
    {
        lock (_gate)
        {
            var next = change(_settings);
            Store.Save(next);
            Volatile.Write(ref _settings, next);
        }

        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Add(MetricDescriptor metric) => Change(settings => settings with
    {
        Slots = settings.Slots.Append(new DisplaySlot(Guid.NewGuid().ToString("N"), metric.Id,
            ShortLabel(metric), settings.Slots.Length, LastKnownMetric: metric)).ToArray(),
    });

    public void Bind(string slotId, MetricDescriptor metric) => Change(settings => settings with
    {
        Slots = settings.Slots.Select(slot => slot.Id == slotId
            ? slot with { MetricId = metric.Id, LastKnownMetric = metric } : slot).ToArray(),
    });

    public void Remove(string slotId) => Change(settings => settings with
    {
        Slots = settings.Slots.Where(slot => slot.Id != slotId).OrderBy(slot => slot.Order)
            .Select((slot, index) => slot with { Order = index }).ToArray(),
    });

    public void Move(string slotId, int direction) => Change(settings =>
    {
        var slots = settings.Slots.OrderBy(slot => slot.Order).ToList();
        var index = slots.FindIndex(slot => slot.Id == slotId);
        var target = index + direction;
        if (index >= 0 && target >= 0 && target < slots.Count)
        {
            (slots[index], slots[target]) = (slots[target], slots[index]);
        }

        return settings with { Slots = slots.Select((slot, order) => slot with { Order = order }).ToArray() };
    });

    public (MetricDescriptor? Descriptor, MetricSample? Sample) Resolve(string? metricId, string? slotId = null)
    {
        var slot = Settings.Slots.FirstOrDefault(slot => slot.Id == slotId);
        metricId = slot?.MetricId ?? metricId;
        var snapshot = Runtime.Snapshot;
        var descriptor = snapshot.Descriptors.FirstOrDefault(metric => metric.Id == metricId);
        var sample = snapshot.Samples.FirstOrDefault(sample => sample.MetricId == metricId);
        if (descriptor is null && slot?.LastKnownMetric is not null)
        {
            descriptor = slot.LastKnownMetric;
            sample = new(descriptor.Id, null, DateTimeOffset.UtcNow, MetricStatus.Unavailable, "device-not-connected");
        }

        return (descriptor, sample);
    }

    private static string ShortLabel(MetricDescriptor metric)
    {
        var name = metric.Name.Split(" / ").Last();
        return name.Length <= 32 ? name : name[..32];
    }
}