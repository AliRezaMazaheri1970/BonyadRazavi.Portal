using System.Collections.Concurrent;
using Microsoft.Extensions.Options;

namespace BonyadRazavi.Auth.Api.Security;

public sealed class InMemoryLoginLockoutService : ILoginLockoutService
{
    private readonly ConcurrentDictionary<string, AttemptState> _attempts = new(StringComparer.OrdinalIgnoreCase);
    private readonly LoginLockoutOptions _options;
    private int _operations;

    public InMemoryLoginLockoutService(IOptions<LoginLockoutOptions> options)
    {
        _options = options.Value;
        if (_options.MaxFailedAttempts < 1)
        {
            _options.MaxFailedAttempts = 5;
        }

        if (_options.LockoutMinutes < 1)
        {
            _options.LockoutMinutes = 15;
        }

        if (_options.EntryTtlMinutes < 10)
        {
            _options.EntryTtlMinutes = 120;
        }
    }

    public LockoutStatus GetStatus(string userName, string clientIp)
    {
        var key = BuildKey(userName, clientIp);
        if (!_attempts.TryGetValue(key, out var state))
        {
            return LockoutStatus.None;
        }

        var now = DateTimeOffset.UtcNow;
        if (state.LockedUntilUtc.HasValue && state.LockedUntilUtc.Value > now)
        {
            return new LockoutStatus(true, state.LockedUntilUtc.Value - now);
        }

        return LockoutStatus.None;
    }

    public LockoutStatus RegisterFailure(string userName, string clientIp)
    {
        var key = BuildKey(userName, clientIp);
        var now = DateTimeOffset.UtcNow;

        while (true)
        {
            if (!_attempts.TryGetValue(key, out var current))
            {
                var initial = ApplyThreshold(new AttemptState(1, null, now), now);
                if (_attempts.TryAdd(key, initial))
                {
                    RunCleanupIfNeeded(now);
                    return ToLockoutStatus(initial, now);
                }

                continue;
            }

            var updated = NextFailureState(current, now);
            if (_attempts.TryUpdate(key, updated, current))
            {
                RunCleanupIfNeeded(now);
                return ToLockoutStatus(updated, now);
            }
        }
    }

    public void RegisterSuccess(string userName, string clientIp)
    {
        var key = BuildKey(userName, clientIp);
        _attempts.TryRemove(key, out _);
    }

    private AttemptState NextFailureState(AttemptState current, DateTimeOffset now)
    {
        if (current.LockedUntilUtc.HasValue && current.LockedUntilUtc.Value > now)
        {
            return current with { UpdatedAtUtc = now };
        }

        var failures = current.FailedAttempts + 1;
        var next = current with
        {
            FailedAttempts = failures,
            LockedUntilUtc = null,
            UpdatedAtUtc = now
        };

        return ApplyThreshold(next, now);
    }

    private AttemptState ApplyThreshold(AttemptState state, DateTimeOffset now)
    {
        if (state.FailedAttempts < _options.MaxFailedAttempts)
        {
            return state;
        }

        return state with
        {
            FailedAttempts = 0,
            LockedUntilUtc = now.AddMinutes(_options.LockoutMinutes),
            UpdatedAtUtc = now
        };
    }

    private static LockoutStatus ToLockoutStatus(AttemptState state, DateTimeOffset now)
    {
        if (state.LockedUntilUtc.HasValue && state.LockedUntilUtc.Value > now)
        {
            return new LockoutStatus(true, state.LockedUntilUtc.Value - now);
        }

        return LockoutStatus.None;
    }

    private void RunCleanupIfNeeded(DateTimeOffset now)
    {
        if (Interlocked.Increment(ref _operations) % 200 != 0)
        {
            return;
        }

        var threshold = now.AddMinutes(-_options.EntryTtlMinutes);
        foreach (var (key, state) in _attempts)
        {
            var shouldRemove = state.LockedUntilUtc.HasValue
                ? state.LockedUntilUtc.Value < threshold
                : state.UpdatedAtUtc < threshold;
            if (shouldRemove)
            {
                _attempts.TryRemove(key, out _);
            }
        }
    }

    private static string BuildKey(string userName, string clientIp)
    {
        var normalizedUserName = userName?.Trim().ToUpperInvariant() ?? string.Empty;
        var normalizedIp = clientIp?.Trim() ?? string.Empty;
        return $"{normalizedIp}|{normalizedUserName}";
    }

    private sealed record AttemptState(
        int FailedAttempts,
        DateTimeOffset? LockedUntilUtc,
        DateTimeOffset UpdatedAtUtc);
}
