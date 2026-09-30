using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using CmdPalDockStats.Extension.Pages;
using CmdPalDockStats.Extension.Services;

namespace CmdPalDockStats.Extension;

public sealed partial class DockStatsCommandsProvider : CommandProvider
{
    public const string BandId = "net.poychang.cmdpaldockstats.band.main";
    public const string SlotPrefix = "net.poychang.cmdpaldockstats.slot.";
    private readonly StatsController _controller;
    private readonly MyStatsPage _stats;
    private readonly MetricExplorerPage _explorer;
    private readonly ICommandItem _band;
    private readonly ICommandItem[] _commands;

    public DockStatsCommandsProvider()
    {
        Id = "net.poychang.cmdpaldockstats";
        DisplayName = "CmdPalDockStats";
        Icon = new IconInfo("\uE9D9");
        _controller = new StatsController();
        _stats = new MyStatsPage(_controller);
        _explorer = new MetricExplorerPage(_controller);
        var diagnostics = new DiagnosticsPage(_controller);
        var settings = new PreferencesPage(_controller);
        var root = new HomePage(_stats, _explorer, settings, diagnostics);
        _band = new CommandItem(_stats) { Title = "My Stats", Icon = Icon };
        _commands = [new CommandItem(root) { Title = DisplayName, Subtitle = "System metrics and hardware temperatures", Icon = Icon }];
        _controller.Runtime.Start();
    }

    public override ICommandItem[] TopLevelCommands() => _commands;
    public override ICommandItem[] GetDockBands() => [_band];
    public override ICommandItem? GetCommandItem(string id)
    {
        if (id == BandId) { return _band; }
        if (id.StartsWith(SlotPrefix, StringComparison.Ordinal))
        {
            return new CommandItem(new MetricDetailsPage(_controller, null, id[SlotPrefix.Length..])) { Title = "My Stats metric" };
        }

        return _commands.FirstOrDefault(item => item.Command.Id == id);
    }

    public override void Dispose()
    {
        _controller.Runtime.Dispose();
        _stats.Dispose();
        _explorer.Dispose();
        base.Dispose();
    }
}