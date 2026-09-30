using System.Diagnostics;
using System.IO.Pipes;
using System.Security.Principal;
using CmdPalDockStats.Contracts;
using CmdPalDockStats.Windows.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CmdPalDockStats.IntegrationTests;

[TestClass]
public sealed class SensorHostTests
{
    [TestMethod]
    [TestCategory("Hardware")]
    public async Task RealSensorHostAcceptsSameSessionAndRecoversFromInvalidRequest()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Directory.Build.props"))) { root = root.Parent; }
        Assert.IsNotNull(root);
        var config = Directory.GetFiles(Path.Combine(root.FullName, "src", "CmdPalDockStats.SensorHost", "bin"),
            "CmdPalDockStats.SensorHost.runtimeconfig.json", SearchOption.AllDirectories).OrderByDescending(File.GetLastWriteTimeUtc).First();
        var assembly = config.Replace(".runtimeconfig.json", ".dll", StringComparison.Ordinal);
        var start = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        start.ArgumentList.Add(assembly);
        using var process = Process.Start(start)!;
        try
        {
            var ready = await process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(30));
            Assert.IsTrue(ready?.Contains("SensorHost ready") == true, ready);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var sid = WindowsIdentity.GetCurrent().User!.Value;
            var session = Process.GetCurrentProcess().SessionId;
            await using (var pipe = new NamedPipeClientStream(".", $"CmdPalDockStats.v1.{sid}.{session}", PipeDirection.InOut,
                PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly, TokenImpersonationLevel.Identification))
            {
                await pipe.ConnectAsync(timeout.Token);
                await SensorProtocol.WriteAsync(pipe, new SensorRequest(99, "snapshot"), timeout.Token);
                await Assert.ThrowsAsync<Exception>(async () => await SensorProtocol.ReadAsync<SensorResponse>(pipe, timeout.Token));
            }

            using var client = new SensorHostClient();
            var snapshot = await client.SampleAsync(timeout.Token);
            Assert.IsTrue(snapshot.Descriptors.Length > 0);
            var temperatures = snapshot.Descriptors.Where(metric => metric.Kind == "gpu.temperature").ToArray();
            Assert.IsTrue(temperatures.Length > 0, "Run Hardware tests only on a GPU supported by LHM.");
            Assert.IsTrue(temperatures.Any(metric => snapshot.Samples.Any(sample => sample.MetricId == metric.Id &&
                sample.Status == MetricStatus.Available && sample.Value.HasValue)), "A real GPU temperature must survive the IPC round trip.");
            foreach (var metric in temperatures)
            {
                var sample = snapshot.Samples.Single(sample => sample.MetricId == metric.Id);
                Console.WriteLine($"{metric.Name}: {sample.Value} C / {sample.Status}");
            }
        }
        finally
        {
            if (!process.HasExited) { process.Kill(true); }
            await process.WaitForExitAsync();
            Console.WriteLine(await process.StandardError.ReadToEndAsync());
        }
    }
}