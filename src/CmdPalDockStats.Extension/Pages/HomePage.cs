using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace CmdPalDockStats.Extension.Pages;

internal sealed partial class HomePage : ListPage
{
    private readonly IListItem[] _items;

    public HomePage(MyStatsPage stats, MetricExplorerPage explorer, PreferencesPage preferences, DiagnosticsPage diagnostics)
    {
        Id = "net.poychang.cmdpaldockstats.home";
        Name = "CmdPalDockStats";
        Icon = new IconInfo("\uE9D9");
        _items =
        [
            new ListItem(stats) { Title = "My Stats", Icon = new IconInfo("\uE9D9") },
            new ListItem(explorer) { Title = "Sensor Explorer", Icon = new IconInfo("\uE9CA") },
            new ListItem(preferences) { Title = "Preferences", Icon = new IconInfo("\uE713") },
            new ListItem(diagnostics) { Title = "Diagnostics", Icon = new IconInfo("\uE946") },
        ];
    }

    public override IListItem[] GetItems() => _items;
}