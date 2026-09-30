using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using CmdPalDockStats.Core.Models;
using CmdPalDockStats.Extension.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace CmdPalDockStats.Extension.Pages;

internal sealed partial class PreferencesPage : ContentPage
{
    private readonly StatsController _controller;

    public PreferencesPage(StatsController controller)
    {
        _controller = controller;
        Id = "net.poychang.cmdpaldockstats.preferences";
        Name = "Preferences";
        Icon = new IconInfo("\uE713");
    }

    public override IContent[] GetContent() => [new SettingsForm(_controller, null)];
}

internal sealed partial class SlotEditPage : ContentPage
{
    private readonly StatsController _controller;
    private readonly string _slotId;

    public SlotEditPage(StatsController controller, string slotId)
    {
        _controller = controller;
        _slotId = slotId;
        Name = "Edit display item";
        Id = "net.poychang.cmdpaldockstats.edit." + slotId;
        Icon = new IconInfo("\uE70F");
    }

    public override IContent[] GetContent() => [new SettingsForm(_controller, _slotId)];
}

internal sealed partial class SettingsForm : FormContent
{
    private readonly StatsController _controller;
    private readonly string? _slotId;

    public SettingsForm(StatsController controller, string? slotId)
    {
        _controller = controller;
        _slotId = slotId;
        var settings = controller.Settings;
        var body = new List<object>();
        if (slotId is null)
        {
            body.Add(Choice("temperature", "Temperature", settings.TemperatureUnit, [("Celsius", "C"), ("Fahrenheit", "F")]));
            body.Add(Choice("rate", "Data rate", settings.RateUnit, [("MiB/s", "MiB/s"), ("Mbps", "Mbps")]));
            body.Add(Choice("system", "System sampling", settings.SystemIntervalMs.ToString(CultureInfo.InvariantCulture),
                [("1 second", "1000"), ("2 seconds", "2000"), ("5 seconds", "5000")]));
            body.Add(Choice("sensors", "Sensor sampling", settings.SensorIntervalMs.ToString(CultureInfo.InvariantCulture),
                [("2 seconds", "2000"), ("5 seconds", "5000"), ("10 seconds", "10000")]));
        }
        else
        {
            var slot = settings.Slots.FirstOrDefault(slot => slot.Id == slotId);
            var (descriptor, _) = controller.Resolve(null, slotId);
            body.Add(new { type = "Input.Text", id = "label", label = "Display name", value = slot?.Label ?? "", maxLength = 80, isRequired = true });
            var units = descriptor?.Unit switch
            {
                "C" => new[] { ("Default", "default"), ("Celsius", "C"), ("Fahrenheit", "F") },
                "B/s" => [("Default", "default"), ("MiB/s", "MiB/s"), ("Mbps", "Mbps")],
                _ => [("Default", "default")],
            };
            body.Add(Choice("unit", "Display unit", slot?.Unit ?? "default", units));
        }

        TemplateJson = JsonSerializer.Serialize(new
        {
            type = "AdaptiveCard", version = "1.5", body,
            actions = new[] { new { type = "Action.Submit", title = "Save" } },
        });
    }

    private static object Choice(string id, string label, string value, (string Title, string Value)[] options) =>
        new { type = "Input.ChoiceSet", id, label, value, style = "compact", choices = options.Select(option => new { title = option.Title, value = option.Value }) };

    public override CommandResult SubmitForm(string payload)
    {
        try
        {
            var data = JsonNode.Parse(payload)?.AsObject() ?? throw new InvalidDataException("Empty form.");
            if (_slotId is null)
            {
                _controller.Change(settings => settings with
                {
                    TemperatureUnit = Required(data, "temperature"), RateUnit = Required(data, "rate"),
                    SystemIntervalMs = int.Parse(Required(data, "system"), CultureInfo.InvariantCulture),
                    SensorIntervalMs = int.Parse(Required(data, "sensors"), CultureInfo.InvariantCulture),
                });
            }
            else
            {
                var label = Required(data, "label").Trim();
                var unit = Required(data, "unit");
                _controller.Change(settings => settings with
                {
                    Slots = settings.Slots.Select(slot => slot.Id == _slotId
                        ? slot with { Label = label, Unit = unit == "default" ? null : unit } : slot).ToArray(),
                });
            }

            return CommandResult.GoBack();
        }
        catch (Exception exception)
        {
            new ToastStatusMessage(exception.Message).Show();
            return CommandResult.KeepOpen();
        }
    }

    private static string Required(JsonObject data, string key) => data[key]?.GetValue<string>()
        ?? throw new InvalidDataException($"Missing {key}.");
}