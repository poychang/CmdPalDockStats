using System.Runtime.InteropServices;
using Microsoft.CommandPalette.Extensions;

namespace CmdPalDockStats.Extension;

[Guid("dc86c7a2-bfde-4405-b090-31d1fe3e04e5")]
public sealed partial class DockStatsExtension(ManualResetEvent disposed) : IExtension, IDisposable
{
    private readonly DockStatsCommandsProvider _provider = new();
    private bool _disposed;

    public object? GetProvider(ProviderType providerType) =>
        providerType == ProviderType.Commands ? _provider : null;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _provider.Dispose();
        disposed.Set();
    }
}