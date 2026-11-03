using System.Collections.Concurrent;

namespace MappingDemo.Api.Services;

public sealed class TableVerificationRateLimiter(
    TimeProvider? timeProvider = null,
    int permitLimit = 12,
    TimeSpan? window = null)
{
    private readonly TimeProvider _timeProvider =
        timeProvider ?? TimeProvider.System;
    private readonly int _permitLimit = permitLimit > 0
        ? permitLimit
        : throw new ArgumentOutOfRangeException(nameof(permitLimit));
    private readonly TimeSpan _window = window ?? TimeSpan.FromMinutes(1);
    private readonly ConcurrentDictionary<long, Queue<DateTimeOffset>>
        _requests = [];

    public bool TryAcquire(long tableId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tableId);

        var requests = _requests.GetOrAdd(tableId, static _ => new());
        var now = _timeProvider.GetUtcNow();
        lock (requests)
        {
            while (requests.TryPeek(out var request) &&
                   now - request >= _window)
            {
                requests.Dequeue();
            }
            if (requests.Count >= _permitLimit)
            {
                return false;
            }
            requests.Enqueue(now);
            return true;
        }
    }
}
