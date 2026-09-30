using Microsoft.CommandPalette.Extensions;
using Shmuelie.WinRTServer;
using Shmuelie.WinRTServer.CsWinRT;

namespace CmdPalDockStats.Extension;

internal static class Program
{
    [MTAThread]
    public static void Main(string[] args)
    {
        if (!args.Contains("-RegisterProcessAsComServer"))
        {
            return;
        }

        using var disposed = new ManualResetEvent(false);
        var extension = new DockStatsExtension(disposed);
        var server = new ComServer();
        server.RegisterClass<DockStatsExtension, IExtension>(() => extension);
        server.Start();
        disposed.WaitOne();
        server.Stop();
        server.UnsafeDispose();
    }
}