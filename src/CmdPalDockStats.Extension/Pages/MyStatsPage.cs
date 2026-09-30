using CmdPalDockStats.Core.Services;
using CmdPalDockStats.Extension.Commands;
using CmdPalDockStats.Extension.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace CmdPalDockStats.Extension.Pages;

internal sealed partial class MyStatsPage : ListPage, IDisposable
{
    private readonly StatsController _controller;
    private readonly object _gate = new();
    private readonly Dictionary<string, ListItem> _items = new();
    private IListItem[] _ordered = [];

    public MyStatsPage(StatsController controller)
    {
        _controller = controller;
        Id = DockStatsCommandsProvider.BandId;
        Name = "My Stats";
        Icon = new IconInfo("\uE9D9");
        EmptyContent = new CommandItem(new MetricExplorerPage(controller, subscribe: false)) { Title = "Add a metric" };
        controller.SettingsChanged += SettingsChanged;
        controller.Runtime.Updated += Update;
        Rebuild();
    }

    public override IListItem[] GetItems() => Volatile.Read(ref _ordered);

    private void SettingsChanged(object? sender, EventArgs args)
    {
        Rebuild();
        RaiseItemsChanged(_ordered.Length);
    }

    private void Rebuild()
    {
        lock (_gate)
        {
            var slots = _controller.Settings.Slots.OrderBy(slot => slot.Order).ToArray();
            foreach (var slot in slots)
            {
                if (_items.ContainsKey(slot.Id)) { continue; }
                var item = new ListItem(new MetricDetailsPage(_controller, null, slot.Id))
                {
                    MoreCommands =
                    [
                        new CommandContextItem(new SlotEditPage(_controller, slot.Id)) { Title = "Rename / display unit", Icon = new IconInfo("\uE70F") },
                        new CommandContextItem(new MetricExplorerPage(_controller, slot.Id, false)) { Title = "Change source", Icon = new IconInfo("\uE8AB") },
                        new CommandContextItem(StatsCommands.Action("Move up", () => _controller.Move(slot.Id, -1), "\uE74A")),
                        new CommandContextItem(StatsCommands.Action("Move down", () => _controller.Move(slot.Id, 1), "\uE74B")),
                        new CommandContextItem(StatsCommands.Action("Remove", () => _controller.Remove(slot.Id), "\uE74D")),
                    ],
                };
                _items[slot.Id] = item;
            }

            foreach (var id in _items.Keys.Where(id => !slots.Any(slot => slot.Id == id)).ToArray())
            {
                _items.Remove(id);
            }

            Volatile.Write(ref _ordered, slots.Select(slot => (IListItem)_items[slot.Id]).ToArray());
        }

        Update(this, EventArgs.Empty);
    }

    private void Update(object? sender, EventArgs args)
    {
        lock (_gate)
        {
            foreach (var slot in _controller.Settings.Slots)
            {
                if (!_items.TryGetValue(slot.Id, out var item)) { continue; }
                var (descriptor, sample) = _controller.Resolve(null, slot.Id);
                var value = sample?.ReasonCode == "device-not-connected" ? "Device not connected" :
                    MetricFormatter.Format(descriptor, sample, _controller.Settings, slot.Unit);
                var title = $"{slot.Label} {value}";
                if (item.Title != title) { item.Title = title; }
                var subtitle = descriptor?.Name ?? slot.MetricId;
                if (item.Subtitle != subtitle) { item.Subtitle = subtitle; }
            }
        }
    }

    public void Dispose()
    {
        _controller.SettingsChanged -= SettingsChanged;
        _controller.Runtime.Updated -= Update;
    }
}