using System.Diagnostics;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace CmdPalDockStats.Extension.Commands;

internal static class StatsCommands
{
    public static AnonymousCommand Action(string name, Action action, string glyph = "\uE8FB") => new(() =>
    {
        try { action(); }
        catch (Exception exception) { new ToastStatusMessage(exception.Message).Show(); }
    }) { Name = name, Icon = new IconInfo(glyph), Result = CommandResult.KeepOpen() };

    public static void StartSensorHost(bool elevated)
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "CmdPalDockStats", "SensorHost", "CmdPalDockStats.SensorHost.exe");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Install SensorHost in Program Files first. See packaging/README.md.");
        }

        Process.Start(new ProcessStartInfo(path)
        {
            UseShellExecute = true,
            Verb = elevated ? "runas" : "open",
            WorkingDirectory = Path.GetDirectoryName(path),
        });
    }
}