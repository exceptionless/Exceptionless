using Microsoft.Extensions.Time.Testing;

namespace Exceptionless.Tests.Utility;

public sealed class ProxyTimeProvider : TimeProvider
{
    private TimeProvider _timeProvider;
    public DateTimeOffset? Start { get; private set; }

    public ProxyTimeProvider(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? System;
    }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) => _timeProvider.CreateTimer(callback, state, dueTime, period);
    public override long GetTimestamp() => _timeProvider.GetTimestamp();
    public override long TimestampFrequency => _timeProvider.TimestampFrequency;
    public override TimeZoneInfo LocalTimeZone => _timeProvider.LocalTimeZone;
    public override DateTimeOffset GetUtcNow() => _timeProvider.GetUtcNow();

    public void Restore()
    {
        _timeProvider = System;
    }

    public void Advance(TimeSpan value)
    {
        if (IsTimeTraveling)
        {
            ((FakeTimeProvider)_timeProvider).Advance(value);
        }
        else
        {
            _timeProvider = new FakeTimeProvider(_timeProvider.GetUtcNow().Add(value));
        }
    }

    public void SetUtcNow(DateTimeOffset value)
    {
        if (IsTimeTraveling)
        {
            ((FakeTimeProvider)_timeProvider).SetUtcNow(value);
        }
        else
        {
            Start = new[] { _timeProvider.GetUtcNow(), value }.Min(r => r.UtcDateTime);
            _timeProvider = new FakeTimeProvider(value);
        }
    }

    private bool IsTimeTraveling => _timeProvider is FakeTimeProvider;
}
