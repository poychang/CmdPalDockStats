using System.Security.Principal;
using CmdPalDockStats.Extension.Commands;
using CmdPalDockStats.Extension.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace CmdPalDockStats.Extension.Pages;

internal sealed partial class DiagnosticsPage : ListPage
{
    private readonly StatsController _controller;

    public DiagnosticsPage(StatsController controller)
    {
        _controller = controller;
        Id = "net.poychang.cmdpaldockstats.diagnostics";
        Name = "Diagnostics";
        Icon = new IconInfo("\uE946");
    }

    public override IListItem[] GetItems()
    {
        var snapshot = _controller.Runtime.Snapshot;
        var items = new List<IListItem>
        {
            new ListItem(StatsCommands.Action("Start SensorHost", () => StatsCommands.StartSensorHost(false), "\uE768")),
            new ListItem(StatsCommands.Action("Enable advanced sensors (UAC)", () => StatsCommands.StartSensorHost(true), "\uE72E")),
            MetricDetailsPage.Info("Extension privileges", new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator) ? "Elevated" : "Standard user"),
            MetricDetailsPage.Info("Metrics", $"{snapshot.Descriptors.Length} discovered / {_controller.Settings.Slots.Length} selected"),
            MetricDetailsPage.Info("Last sample (UTC)", snapshot.Samples.Length > 0 ? snapshot.Samples.Max(sample => sample.TimestampUtc).ToString("O") : "none"),
            MetricDetailsPage.Info("Settings", _controller.Store.Diagnostic ?? (_controller.Store.CanWrite ? "Writable / schema 1" : "Read-only")),
        };
        items.AddRange(snapshot.Diagnostics.Select(message => MetricDetailsPage.Info("Provider diagnostic", message)));
        return items.ToArray();
    }
}