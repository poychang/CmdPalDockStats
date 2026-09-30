using CmdPalDockStats.Core.Services;
using CmdPalDockStats.Extension.Commands;
using CmdPalDockStats.Extension.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace CmdPalDockStats.Extension.Pages;

internal sealed partial class MetricDetailsPage : ListPage
{
    private readonly StatsController _controller;
    private readonly string? _metricId;
    private readonly string? _slotId;

    public MetricDetailsPage(StatsController controller, string? metricId, string? slotId = null)
    {
        _controller = controller;
        _metricId = metricId;
        _slotId = slotId;
        Id = slotId is not null ? DockStatsCommandsProvider.SlotPrefix + slotId : "net.poychang.cmdpaldockstats.metric." + metricId;
        Name = "Metric details";
        Icon = new IconInfo("\uE946");
    }

    public override IListItem[] GetItems()
    {
        if (_slotId is not null && !_controller.Settings.Slots.Any(slot => slot.Id == _slotId))
        {
            return [Info("Display item removed", "This pinned Slot ID no longer exists.")];
        }

        var (metric, sample) = _controller.Resolve(_metricId, _slotId);
        if (metric is null) { return [Info("Device not connected", _metricId ?? _slotId ?? "")]; }
        if (sample is not null)
        {
            sample = MetricFormatter.EffectiveSample(sample, DateTimeOffset.UtcNow,
                metric.Provider == "Windows" ? _controller.Settings.SystemIntervalMs : _controller.Settings.SensorIntervalMs);
        }

        var items = new List<IListItem>
        {
            Info(metric.Name, MetricFormatter.Format(metric, sample, _controller.Settings)),
            Info("Status", $"{sample?.Status.ToString() ?? "WarmingUp"} / {sample?.ReasonCode ?? "none"}"),
            Info("Source", metric.Source),
            Info("Last sample (UTC)", sample?.TimestampUtc.ToString("O") ?? "none"),
            Info("Metric ID", metric.Id),
            Info("Device", metric.DeviceKey),
            Info("Sensor", metric.SensorKey),
        };
        if (_slotId is null)
        {
            items.Add(new ListItem(StatsCommands.Action("Add to My Stats", () => _controller.Add(metric), "\uE710")));
        }
        else
        {
            items.Add(new ListItem(new SlotEditPage(_controller, _slotId)) { Title = "Rename / display unit" });
            items.Add(new ListItem(new MetricExplorerPage(_controller, _slotId, false)) { Title = "Change source" });
        }

        return items.ToArray();
    }

    internal static ListItem Info(string title, string subtitle) => new(new NoOpCommand()) { Title = title, Subtitle = subtitle };
}