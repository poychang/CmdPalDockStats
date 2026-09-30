using System.Diagnostics;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Runtime.InteropServices;
using CmdPalDockStats.Contracts;

namespace CmdPalDockStats.SensorHost;

internal static class Program
{
    public static async Task Main(string[] args)
    {
        using var sampler = new HardwareSampler();
        if (args.Contains("--once"))
        {
            var snapshot = sampler.Sample();
            Console.WriteLine($"LHM 0.9.6: {snapshot.Descriptors.Length} metrics; elevated: {IsElevated()}");
            foreach (var descriptor in snapshot.Descriptors.Where(metric => metric.Unit == "C"))
            {
                var sample = snapshot.Samples.First(value => value.MetricId == descriptor.Id);
                Console.WriteLine($"{descriptor.Name}: {sample.Value?.ToString("F1") ?? "unavailable"} C ({sample.Status})");
            }

            foreach (var diagnostic in snapshot.Diagnostics)
            {
                Console.WriteLine(diagnostic);
            }

            return;
        }

        var sid = WindowsIdentity.GetCurrent().User?.Value ?? throw new InvalidOperationException("No user SID.");
        var session = Process.GetCurrentProcess().SessionId;
        var name = $"CmdPalDockStats.v{SensorProtocol.Version}.{sid}.{session}";
        var security = new PipeSecurity();
        security.SetAccessRuleProtection(true, false);
        security.SetOwner(new SecurityIdentifier(sid));
        security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.NetworkSid, null),
            PipeAccessRights.FullControl, AccessControlType.Deny));
        security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(sid), PipeAccessRights.FullControl, AccessControlType.Allow));
        security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
            PipeAccessRights.FullControl, AccessControlType.Allow));
        Console.WriteLine($"SensorHost ready; elevated: {IsElevated()}; idle exit: 60 seconds.");
        var cached = MetricSnapshot.Empty;
        var lastSample = Stopwatch.StartNew();
        while (true)
        {
            await using var pipe = NamedPipeServerStreamAcl.Create(name, PipeDirection.InOut, 1,
                PipeTransmissionMode.Byte, PipeOptions.Asynchronous, 4096, 4096, security);
            using var idle = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            try
            {
                await pipe.WaitForConnectionAsync(idle.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try
            {
                var request = await SensorProtocol.ReadAsync<SensorRequest>(pipe, timeout.Token);
                VerifyClient(pipe, sid, session);
                SensorProtocol.Validate(request);
                if (cached == MetricSnapshot.Empty || lastSample.Elapsed >= TimeSpan.FromSeconds(2))
                {
                    cached = sampler.Sample();
                    lastSample.Restart();
                }

                await SensorProtocol.WriteAsync(pipe, new SensorResponse(SensorProtocol.Version, cached), timeout.Token);
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or
                OperationCanceledException or System.Text.Json.JsonException)
            {
                Console.Error.WriteLine($"Rejected request: {exception.GetType().Name}: {exception.Message}");
            }
        }
    }

    private static bool IsElevated() => new WindowsPrincipal(WindowsIdentity.GetCurrent())
        .IsInRole(WindowsBuiltInRole.Administrator);

    private static void VerifyClient(NamedPipeServerStream pipe, string sid, int session)
    {
        if (!GetNamedPipeClientSessionId(pipe.SafePipeHandle, out var clientSession) || clientSession != session)
        {
            throw new UnauthorizedAccessException("Only the same local session is allowed.");
        }

        string? clientSid = null;
        pipe.RunAsClient(() => clientSid = WindowsIdentity.GetCurrent(true)?.User?.Value);
        if (clientSid != sid)
        {
            throw new UnauthorizedAccessException("Client SID mismatch.");
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetNamedPipeClientSessionId(Microsoft.Win32.SafeHandles.SafePipeHandle pipe, out uint session);
}