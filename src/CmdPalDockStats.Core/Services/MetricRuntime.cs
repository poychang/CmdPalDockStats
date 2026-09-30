using CmdPalDockStats.Contracts;
using CmdPalDockStats.Core.Models;

namespace CmdPalDockStats.Core.Services;

public interface IMetricProvider : IDisposable
{
    string Name { get; }
    Task<MetricSnapshot> SampleAsync(CancellationToken cancellationToken);
}

public sealed class MetricRuntime : IDisposable
{
    private readonly IMetricProvider[] _providers;
    private readonly Func<AppSettings> _settings;
    private readonly CancellationTokenSource _cancellation = new();
    private readonly Dictionary<string, MetricSnapshot> _snapshots = new();
    private readonly Dictionary<string, TimeSpan> _nextPoll = new();
    private readonly Dictionary<string, int> _failures = new();
    private Task? _loop;
    private MetricSnapshot _snapshot = MetricSnapshot.Empty;
    private bool _disposed;

    public MetricRuntime(IMetricProvider[] providers, Func<AppSettings> settings)
    {
        _providers = providers;
        _settings = settings;
    }

    public MetricSnapshot Snapshot => Volatile.Read(ref _snapshot);
    public event EventHandler? Updated;

    public void Start() => _loop ??= Task.Run(RunAsync);

    private async Task RunAsync()
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        var clock = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            do
            {
                foreach (var provider in _providers)
                {
                    if (_nextPoll.GetValueOrDefault(provider.Name) > clock.Elapsed)
                    {
                        continue;
                    }

                    var settings = _settings();
                    var interval = provider.Name == "Windows" ? settings.SystemIntervalMs : settings.SensorIntervalMs;
                    try
                    {
                        _snapshots[provider.Name] = await provider.SampleAsync(_cancellation.Token);
                        _failures[provider.Name] = 0;
                    }
                    catch (OperationCanceledException) when (_cancellation.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception exception)
                    {
                        var old = _snapshots.GetValueOrDefault(provider.Name, MetricSnapshot.Empty);
                        _snapshots[provider.Name] = old with
                        {
                            Samples = old.Samples.Select(sample => sample with
                            {
                                Value = null, Status = MetricStatus.Error, ReasonCode = "provider-failed",
                            }).ToArray(),
                            Diagnostics = [$"{provider.Name}: {exception.GetType().Name}: {exception.Message}"],
                        };
                        var failures = Math.Min(_failures.GetValueOrDefault(provider.Name) + 1, 6);
                        _failures[provider.Name] = failures;
                        interval = Math.Min(60000, 1000 * (1 << failures));
                    }

                    _nextPoll[provider.Name] = clock.Elapsed + TimeSpan.FromMilliseconds(interval);
                }

                var snapshot = new MetricSnapshot(
                    _snapshots.Values.SelectMany(value => value.Descriptors).ToArray(),
                    _snapshots.Values.SelectMany(value => value.Samples).ToArray(),
                    _snapshots.Values.SelectMany(value => value.Diagnostics).ToArray());
                Volatile.Write(ref _snapshot, snapshot);
                Updated?.Invoke(this, EventArgs.Empty);
            }
            while (await timer.WaitForNextTickAsync(_cancellation.Token));
        }
        catch (OperationCanceledException) when (_cancellation.IsCancellationRequested)
        {
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _cancellation.Cancel();
        _loop?.GetAwaiter().GetResult();
        foreach (var provider in _providers)
        {
            provider.Dispose();
        }

        _cancellation.Dispose();
    }
}