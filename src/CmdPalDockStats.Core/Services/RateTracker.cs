namespace CmdPalDockStats.Core.Services;

public sealed class RateTracker
{
    private long? _previous;
    private TimeSpan _previousTime;

    public double? Next(long counter, TimeSpan monotonicTime)
    {
        var elapsed = monotonicTime - _previousTime;
        var previous = _previous;
        _previous = counter;
        _previousTime = monotonicTime;
        if (previous is null || counter < 0 || previous < 0 || counter < previous ||
            elapsed <= TimeSpan.Zero || elapsed > TimeSpan.FromSeconds(30))
        {
            return null;
        }

        return (counter - previous.Value) / elapsed.TotalSeconds;
    }
}