using System.Text.Json;
using CmdPalDockStats.Contracts;
using CmdPalDockStats.Core.Models;
using CmdPalDockStats.Core.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CmdPalDockStats.Tests;

[TestClass]
public sealed class CoreTests
{
    [TestMethod]
    public void RatesWarmUpResetAndResumeWithoutSpikes()
    {
        var tracker = new RateTracker();
        Assert.IsNull(tracker.Next(100, TimeSpan.Zero));
        Assert.AreEqual(50d, tracker.Next(200, TimeSpan.FromSeconds(2)));
        Assert.AreEqual(0d, tracker.Next(200, TimeSpan.FromSeconds(3)));
        Assert.IsNull(tracker.Next(10, TimeSpan.FromSeconds(4)));
        Assert.IsNull(tracker.Next(10000, TimeSpan.FromSeconds(40)));
        Assert.AreEqual(10d, tracker.Next(10010, TimeSpan.FromSeconds(41)));
    }

    [TestMethod]
    public void FormattingPreservesZeroAndDoesNotExposeUnavailableNumbers()
    {
        var descriptor = new MetricDescriptor("temperature", "LHM", "cpu", "package", "Package", "cpu.temperature", "C", "LHM");
        var sample = new MetricSample(descriptor.Id, 0, DateTimeOffset.UtcNow, MetricStatus.Available);
        Assert.AreEqual("0.0\u00b0C", MetricFormatter.Format(descriptor, sample, new()));
        Assert.AreEqual("32.0\u00b0F", MetricFormatter.Format(descriptor, sample, new() { TemperatureUnit = "F" }));
        Assert.AreEqual("Unavailable", MetricFormatter.Format(descriptor, sample with { Value = null, Status = MetricStatus.Unavailable }, new()));
        Assert.AreEqual("Stale", MetricFormatter.Format(descriptor, sample with { TimestampUtc = DateTimeOffset.UtcNow.AddMinutes(-1) }, new()));
        Assert.AreEqual("Device not connected", MetricFormatter.Format(null, sample, new()));
    }

    [TestMethod]
    public void SettingsRoundTripAndCorruptionPreserveLastValidBackup()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "settings.json");
        try
        {
            var store = new SettingsStore(path);
            var settings = new AppSettings();
            store.Save(settings);
            store.Save(settings with { TemperatureUnit = "F" });
            Assert.AreEqual("F", store.Load().TemperatureUnit);
            File.WriteAllText(path, "broken json");
            var recovered = new SettingsStore(path);
            Assert.AreEqual("C", recovered.Load().TemperatureUnit);
            Assert.AreEqual(1, Directory.GetFiles(directory, "*.corrupt-*").Length);
            recovered.Save(settings);
            Assert.AreEqual("C", new SettingsStore(path).Load().TemperatureUnit);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [TestMethod]
    public void UnknownSchemaIsNeverOverwritten()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "{\"schemaVersion\":99}");
            var store = new SettingsStore(path);
            store.Load();
            Assert.IsFalse(store.CanWrite);
            Assert.ThrowsExactly<InvalidOperationException>(() => store.Save(new()));
            Assert.AreEqual("{\"schemaVersion\":99}", File.ReadAllText(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void SlotsKeepIdentityAndRejectDuplicatesOrHighFrequency()
    {
        var settings = new AppSettings();
        var renamed = settings.Slots[0] with { Label = "Processor", Order = 1 };
        Assert.AreEqual(settings.Slots[0].Id, renamed.Id);
        Assert.ThrowsExactly<InvalidDataException>(() => (settings with { Slots = [renamed, renamed] }).Validate());
        Assert.ThrowsExactly<InvalidDataException>(() => (settings with { SystemIntervalMs = 1 }).Validate());
    }

    [TestMethod]
    public async Task ProtocolRejectsOversizeAndVersionMismatch()
    {
        using var stream = new MemoryStream(BitConverter.GetBytes(SensorProtocol.MaxMessageBytes + 1));
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => SensorProtocol.ReadAsync<SensorRequest>(stream, CancellationToken.None));
        Assert.ThrowsExactly<InvalidDataException>(() => SensorProtocol.Validate(new SensorRequest(99, "snapshot")));
        Assert.ThrowsExactly<InvalidDataException>(() => SensorProtocol.Validate(new SensorRequest(1, "execute")));
    }

    [TestMethod]
    public async Task ProtocolRoundTripsReadOnlySnapshot()
    {
        using var stream = new MemoryStream();
        var expected = new SensorResponse(SensorProtocol.Version, MetricSnapshot.Empty);
        await SensorProtocol.WriteAsync(stream, expected, CancellationToken.None);
        stream.Position = 0;
        var actual = await SensorProtocol.ReadAsync<SensorResponse>(stream, CancellationToken.None);
        SensorProtocol.Validate(actual);
        Assert.AreEqual(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));
    }
}