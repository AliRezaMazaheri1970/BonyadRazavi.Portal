using Microsoft.Extensions.Options;

namespace BonyadRazavi.Auth.Api.Security;

public sealed class InMemoryLoginFailureAlertService : ILoginFailureAlertService
{
    private readonly Dictionary<string, FailureState> _states = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _sync = new();
    private readonly LoginFailureAlertOptions _options;
    private readonly ILogger<InMemoryLoginFailureAlertService> _logger;

    public InMemoryLoginFailureAlertService(
        IOptions<LoginFailureAlertOptions> options,
        ILogger<InMemoryLoginFailureAlertService> logger)
    {
        _options = options.Value;
        _logger = logger;

        if (_options.FailedAttemptsThreshold < 1)
        {
            _options.FailedAttemptsThreshold = 20;
        }

        if (_options.WindowMinutes < 1)
        {
            _options.WindowMinutes = 10;
        }

        if (_options.SuppressionMinutes < 1)
        {
            _options.SuppressionMinutes = 15;
        }
    }

    public void RegisterFailure(string userName, string clientIp)
    {
        var now = DateTime.UtcNow;
        var key = BuildKey(userName, clientIp);

        lock (_sync)
        {
            if (!_states.TryGetValue(key, out var state) || now > state.WindowEndsAtUtc)
            {
                state = new FailureState
                {
                    Count = 0,
                    WindowEndsAtUtc = now.AddMinutes(_options.WindowMinutes)
                };
            }

            state.Count++;

            if (state.Count >= _options.FailedAttemptsThreshold &&
                (state.LastAlertAtUtc is null ||
                 now - state.LastAlertAtUtc.Value >= TimeSpan.FromMinutes(_options.SuppressionMinutes)))
            {
                _logger.LogWarning(
                    "Suspicious login failures detected. UserName: {UserName}, ClientIp: {ClientIp}, Count: {Count}, WindowMinutes: {WindowMinutes}",
                    userName,
                    clientIp,
                    state.Count,
                    _options.WindowMinutes);

                state.LastAlertAtUtc = now;
            }

            _states[key] = state;
        }
    }

    public void RegisterSuccess(string userName, string clientIp)
    {
        var key = BuildKey(userName, clientIp);
        lock (_sync)
        {
            _states.Remove(key);
        }
    }

    private static string BuildKey(string userName, string clientIp)
    {
        return $"{userName.Trim().ToLowerInvariant()}|{clientIp.Trim()}";
    }

    private sealed class FailureState
    {
        public int Count { get; set; }
        public DateTime WindowEndsAtUtc { get; set; }
        public DateTime? LastAlertAtUtc { get; set; }
    }
}
