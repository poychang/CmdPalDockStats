using CmdPalDockStats.Contracts;
using CmdPalDockStats.Core.Models;
using CmdPalDockStats.Core.Services;
using CmdPalDockStats.Windows.Providers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CmdPalDockStats.IntegrationTests;

[TestClass]
public sealed class ProviderTests
{
    [TestMethod]
    [TestCategory("Windows")]
    public async Task WindowsReadsRealMemoryAndWarmsUpCounters()
    {
        using var provider = new WindowsMetricProvider();
        var first = await provider.SampleAsync(CancellationToken.None);
        var memory = first.Samples.Single(sample => sample.MetricId == "windows/memory/physical-used-percent");
        Assert.AreEqual(MetricStatus.Available, memory.Status);
        Assert.IsTrue(memory.Value >= 0 && memory.Value <= 100);
        Assert.AreEqual(MetricStatus.WarmingUp, first.Samples.Single(sample => sample.MetricId == "windows/cpu/usage").Status);
        await Task.Delay(1100);
        var second = await provider.SampleAsync(CancellationToken.None);
        var cpu = second.Samples.Single(sample => sample.MetricId == "windows/cpu/usage");
        Assert.AreEqual(MetricStatus.Available, cpu.Status, string.Join("; ", second.Diagnostics));
        Assert.IsTrue(cpu.Value >= 0 && cpu.Value <= 100);
        Assert.IsTrue(second.Samples.Where(sample => sample.Value.HasValue).All(sample => double.IsFinite(sample.Value!.Value)));
        Assert.IsTrue(second.Descriptors.Any(metric => metric.Kind == "disk.read"), "Physical disk PDH array must be readable on this test machine.");
    }

    [TestMethod]
    public async Task AFailingProviderDoesNotStopTheHealthyProvider()
    {
        using var runtime = new MetricRuntime([new FakeProvider("Windows", false), new FakeProvider("SensorHost", true)], () => new AppSettings());
        var updated = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        runtime.Updated += (_, _) => updated.TrySetResult();
        runtime.Start();
        await updated.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.AreEqual(42d, runtime.Snapshot.Samples.Single().Value);
        Assert.IsTrue(runtime.Snapshot.Diagnostics.Any(message => message.Contains("SensorHost")));
    }

    private sealed class FakeProvider(string name, bool fail) : IMetricProvider
    {
        public string Name => name;
        public Task<MetricSnapshot> SampleAsync(CancellationToken cancellationToken)
        {
            if (fail) { throw new IOException("Simulated provider failure"); }
            return Task.FromResult(new MetricSnapshot([], [new("test", 42, DateTimeOffset.UtcNow, MetricStatus.Available)], []));
        }
        public void Dispose() { }
    }
}