namespace MappingDemo.Shared.Delivery;

public enum DeliveryFailureKind
{
    Delivered,
    Retryable,
    Permanent
}

public static class DeliveryRetryPolicy
{
    private static readonly TimeSpan[] Backoff =
    [
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(30),
        TimeSpan.FromMinutes(2),
        TimeSpan.FromMinutes(10),
        TimeSpan.FromMinutes(30)
    ];

    public const int MaximumAttempts = 6;

    public static DeliveryFailureKind Classify(int? statusCode, bool transportFailure)
    {
        if (transportFailure)
        {
            return DeliveryFailureKind.Retryable;
        }

        if (statusCode is >= 200 and <= 299)
        {
            return DeliveryFailureKind.Delivered;
        }

        return statusCode is 408 or 429 or >= 500
            ? DeliveryFailureKind.Retryable
            : DeliveryFailureKind.Permanent;
    }

    public static TimeSpan Delay(
        int completedAttempts,
        TimeSpan? retryAfter = null,
        double jitterFactor = 1)
    {
        if (completedAttempts < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(completedAttempts));
        }

        var baseDelay = Backoff[Math.Min(completedAttempts - 1, Backoff.Length - 1)];
        if (retryAfter is { } requested && requested > baseDelay)
        {
            baseDelay = requested;
        }

        var bounded = baseDelay > TimeSpan.FromMinutes(30)
            ? TimeSpan.FromMinutes(30)
            : baseDelay;
        var boundedJitter = Math.Clamp(jitterFactor, 0.5, 1.5);
        var jittered = TimeSpan.FromMilliseconds(
            bounded.TotalMilliseconds * boundedJitter);
        return jittered > TimeSpan.FromMinutes(30)
            ? TimeSpan.FromMinutes(30)
            : jittered;
    }
}
