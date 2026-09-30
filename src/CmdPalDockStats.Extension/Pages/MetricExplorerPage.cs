using CmdPalDockStats.Core.Services;
using CmdPalDockStats.Extension.Commands;
using CmdPalDockStats.Extension.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace CmdPalDockStats.Extension.Pages;

internal sealed partial class MetricExplorerPage : ListPage, IDisposable
{
    private readonly StatsController _controller;
    private readonly string? _slotId;
    private readonly object _gate = new();
    private readonly Dictionary<string, ListItem> _items = new();
    private string[] _ids = [];
    private IListItem[] _ordered = [];

    public MetricExplorerPage(StatsController controller, string? slotId = null, bool subscribe = true)
    {
        _controller = controller;
        _slotId = slotId;
        Id = "net.poychang.cmdpaldockstats.explorer" + (slotId is null ? "" : "." + slotId);
        Name = slotId is null ? "Sensor Explorer" : "Change source";
        Icon = new IconInfo("\uE9CA");
        PlaceholderText = "Search sensors, devices or units";
        if (subscribe) { controller.Runtime.Updated += Update; }
    }

    public override IListItem[] GetItems()
    {
        Update(this, EventArgs.Empty);
        return Volatile.Read(ref _ordered);
    }

    private void Update(object? sender, EventArgs args)
    {
        lock (_gate)
        {
            var snapshot = _controller.Runtime.Snapshot;
            var metrics = snapshot.Descriptors.OrderBy(metric => metric.Unit == "C" ? 0 : 1)
                .ThenBy(metric => metric.Kind).ThenBy(metric => metric.Name).ToArray();
            var ids = metrics.Select(metric => metric.Id).ToArray();
            var changed = !_ids.SequenceEqual(ids);
            foreach (var metric in metrics)
            {
                if (!_items.TryGetValue(metric.Id, out var item))
                {
                    var select = StatsCommands.Action(_slotId is null ? "Add to My Stats" : "Use this source", () =>
                    {
                        if (_slotId is null) { _controller.Add(metric); }
                        else { _controller.Bind(_slotId, metric); }
                    }, "\uE710");
                    item = new ListItem(new MetricDetailsPage(_controller, metric.Id))
                    {
                        Title = metric.Name,
                        Icon = new IconInfo(metric.Unit == "C" ? "\uE9CA" : "\uE9D9"),
                        MoreCommands = [new CommandContextItem(select)],
                    };
                    _items[metric.Id] = item;
                }

                var sample = snapshot.Samples.FirstOrDefault(sample => sample.MetricId == metric.Id);
                var subtitle = $"{MetricFormatter.Format(metric, sample, _controller.Settings)} | {metric.Kind} | {metric.Provider}";
                if (item.Subtitle != subtitle) { item.Subtitle = subtitle; }
            }

            if (changed)
            {
                foreach (var id in _items.Keys.Where(id => !ids.Contains(id)).ToArray()) { _items.Remove(id); }
                _ids = ids;
                Volatile.Write(ref _ordered, ids.Select(id => (IListItem)_items[id]).ToArray());
                RaiseItemsChanged(_ordered.Length);
            }
        }
    }

    public void Dispose() => _controller.Runtime.Updated -= Update;
}