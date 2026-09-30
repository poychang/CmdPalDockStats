using System.Diagnostics;
using System.IO.Pipes;
using System.Security.Principal;
using CmdPalDockStats.Contracts;
using CmdPalDockStats.Core.Services;

namespace CmdPalDockStats.Windows.Services;

public sealed class SensorHostClient : IMetricProvider
{
    public string Name => "SensorHost";

    public async Task<MetricSnapshot> SampleAsync(CancellationToken cancellationToken)
    {
        var sid = WindowsIdentity.GetCurrent().User?.Value ?? throw new InvalidOperationException("No user SID.");
        var session = Process.GetCurrentProcess().SessionId;
        var name = $"CmdPalDockStats.v{SensorProtocol.Version}.{sid}.{session}";
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        await using var pipe = new NamedPipeClientStream(".", name, PipeDirection.InOut,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly, TokenImpersonationLevel.Identification);
        try
        {
            await pipe.ConnectAsync(500, timeout.Token);
            await SensorProtocol.WriteAsync(pipe, new SensorRequest(SensorProtocol.Version, "snapshot"), timeout.Token);
            var response = await SensorProtocol.ReadAsync<SensorResponse>(pipe, timeout.Token);
            SensorProtocol.Validate(response);
            return response.Snapshot;
        }
        catch (Exception exception) when (exception is TimeoutException or IOException ||
            exception is OperationCanceledException && !cancellationToken.IsCancellationRequested)
        {
            throw new IOException("SensorHost is not responding. Start it from Diagnostics; basic Windows metrics remain available.", exception);
        }
    }

    public void Dispose() { }
}